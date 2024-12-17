using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

namespace lab5
{
    internal class RobertsOperator
    {
        private static double[,] _matrixX = new double[,]
        {
            { 1, 0 },
            { 0, -1 }
        };

        private static double[,] _matrixY = new double[,]
        {
            { 0, 1 },
            { -1, 0 }
        };

        private Stopwatch _stopwatch = new Stopwatch();

        public RobertsOperator() { }

        public Bitmap GetResultLinear(Bitmap bitmap)
        {
            _stopwatch.Restart();

            Bitmap output = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);

            // Преобразование исходного изображения в массив оттенков серого
            float[,] pixels = GetGrayscaleArray(bitmap);

            BitmapData outputData = output.LockBits(
                new Rectangle(0, 0, output.Width, output.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            int bytesPerPixel = 4;
            int stride = outputData.Stride;
            byte[] pixelData = new byte[stride * output.Height];

            for (int y = 0; y < output.Height; y++)
            {
                for (int x = 0; x < output.Width; x++)
                {
                    // Рассчитываем градиенты по X и Y
                    double Gx = GetGradient(pixels, x, y, _matrixX);
                    double Gy = GetGradient(pixels, x, y, _matrixY);

                    // Итоговый градиент
                    double grad = Math.Sqrt(Gx * Gx + Gy * Gy);
                    grad = Math.Min(255, grad); // Ограничиваем значение градиента до 255

                    // Вычисляем позицию текущего пикселя в массиве
                    int pixelIndex = y * stride + x * bytesPerPixel;

                    // Записываем значения в массив (градиент для всех цветовых каналов)
                    pixelData[pixelIndex] = (byte)grad;        // Синий канал
                    pixelData[pixelIndex + 1] = (byte)grad;    // Зеленый канал
                    pixelData[pixelIndex + 2] = (byte)grad;    // Красный канал
                    pixelData[pixelIndex + 3] = 255;           // Альфа-канал
                }
            }

            // Копируем обработанные данные обратно в изображение
            Marshal.Copy(pixelData, 0, outputData.Scan0, pixelData.Length);
            output.UnlockBits(outputData);

            _stopwatch.Stop();
            Console.WriteLine($"Линейная обработка заняла: {_stopwatch.ElapsedMilliseconds} ms");

            return output;
        }

        public Bitmap GetResultParallel(Bitmap bitmap)
        {
            int cores = Environment.ProcessorCount;
            /*ThreadPool.SetMaxThreads(100, 100);*/

            _stopwatch.Restart();

            Bitmap output = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
            float[,] pixels = GetGrayscaleArray(bitmap);
            bitmap.Dispose();

            int bytesPerPixel = 4;
            int height = pixels.GetLength(1);
            int width = pixels.GetLength(0);
            int stride = width * bytesPerPixel;

            int blockHeight = 100;

            BitmapData outputData = output.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            int activeThreads = 0; 

            for (int startY = 0; startY < height; startY += blockHeight)
            {
                int currentBlockHeight = Math.Min(blockHeight, height - startY);
                CountdownEvent countdown = new CountdownEvent(cores);

                byte[] pixelDataChunk = new byte[stride * currentBlockHeight];

                for (int i = 0; i < cores; i++)
                {
                    int threadStartY = i * (currentBlockHeight / cores);
                    int threadEndY = (i == cores - 1) ? currentBlockHeight : (i + 1) * (currentBlockHeight / cores);

                    ThreadPool.QueueUserWorkItem((state) =>
                    {
                        Interlocked.Increment(ref activeThreads);
                        try
                        {
                            for (int y = threadStartY; y < threadEndY; y++)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    int localPixelIndex = y * stride + x * bytesPerPixel;

                                    double Gx = GetGradient(pixels, x, startY + y, _matrixX);
                                    double Gy = GetGradient(pixels, x, startY + y, _matrixY);
                                    double grad = Math.Sqrt(Gx * Gx + Gy * Gy);
                                    grad = Math.Min(255, grad);

                                    pixelDataChunk[localPixelIndex] = (byte)grad;
                                    pixelDataChunk[localPixelIndex + 1] = (byte)grad;
                                    pixelDataChunk[localPixelIndex + 2] = (byte)grad;
                                    pixelDataChunk[localPixelIndex + 3] = 255;
                                }
                            }
                        }
                        finally
                        {
                            Interlocked.Decrement(ref activeThreads); 
                            countdown.Signal();
                        }
                    });
                }

                countdown.Wait();

                IntPtr ptr = outputData.Scan0 + startY * stride;
                Marshal.Copy(pixelDataChunk, 0, ptr, pixelDataChunk.Length);
            }

            output.UnlockBits(outputData);

            ThreadPool.GetMaxThreads(out int maxWorkerThreads, out _);

            Console.WriteLine($"Максимальное количество потоков: {maxWorkerThreads}");
            Console.WriteLine($"Используется потоков: {activeThreads}");

            _stopwatch.Stop();
            Console.WriteLine($"Параллельная обработка заняла: {_stopwatch.ElapsedMilliseconds} ms");

            pixels = null;

            return output;
        }



        private static double GetGradient(float[,] pixels, int x, int y, double[,] matrix)
        {
            double grad = 0;

            for (int i = 0; i <= 1; i++)
            {
                for (int j = 0; j <= 1; j++)
                {
                    int newX = Math.Min(Math.Max(x + j, 0), pixels.GetLength(0) - 1);
                    int newY = Math.Min(Math.Max(y + i, 0), pixels.GetLength(1) - 1);

                    grad += pixels[newX, newY] * matrix[i, j];
                }
            }

            return grad;
        }
/*
        public void ComparePerformance(Bitmap image, Channels channel)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            GetResultLinear(image);
            stopwatch.Stop();
            Console.WriteLine($"Линейная обработка заняла: {stopwatch.ElapsedMilliseconds} ms");

            stopwatch.Restart();
            GetResultParallel(image);
            stopwatch.Stop();
            Console.WriteLine($"Параллельная обработка заняла: {stopwatch.ElapsedMilliseconds} ms");
        }*/

        private float[,] GetGrayscaleArray(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;

            float[,] grayscaleArray = new float[width, height];
            PixelFormat pixelFormat = bitmap.PixelFormat;

            BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, pixelFormat);
            int bytesPerPixel = Image.GetPixelFormatSize(bmpData.PixelFormat) / 8;
            int stride = bmpData.Stride;

            int blockHeight = 50;
            byte[] pixelRow = new byte[stride]; 

            for (int startY = 0; startY < height; startY += blockHeight)
            {
                int currentBlockHeight = Math.Min(blockHeight, height - startY);

                for (int y = startY; y < startY + currentBlockHeight; y++)
                {
                    IntPtr rowPtr = bmpData.Scan0 + (y * stride);
                    Marshal.Copy(rowPtr, pixelRow, 0, stride);

                    for (int x = 0; x < width; x++)
                    {
                        int pixelIndex = x * bytesPerPixel;

                        byte blue = pixelRow[pixelIndex];
                        byte green = pixelRow[pixelIndex + 1];
                        byte red = pixelRow[pixelIndex + 2];

                        float gray = 0.3f * red + 0.59f * green + 0.11f * blue;
                        grayscaleArray[x, y] = gray;
                    }
                }
            }

            bitmap.UnlockBits(bmpData);
            pixelRow = null;

            GC.Collect(); 
            return grayscaleArray;
        }
    }
}