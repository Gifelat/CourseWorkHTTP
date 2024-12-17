using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace lab5
{
    internal class HTTPServer
    {
        private static HttpListener _httpServer = new HttpListener();
        private static RobertsOperator _roberts = new RobertsOperator();

        private static string GET_REQUEST = "<html>\r\n<head>\r\n<meta charset=\"utf-8\"/>\r\n<title>PSP-lab5</title>\r\n</head>\r\n  " +
            "<body>\r\n<p>\r\n<input type=\"file\" id=\"fileInput\">\r\n    <button onclick=\"uploadImage()\">Загрузить изображение</button>\r\n\r\n    <script>\r\n        function uploadImage() {\r\n            var fileInput = document.getElementById('fileInput');\r\n            var file = fileInput.files[0];\r\n            var reader = new FileReader();\r\n\r\n            reader.onload = function(e) {\r\n                var imageData = e.target.result;\r\n                sendDataToServer(imageData);\r\n            }\r\n\r\n            reader.readAsDataURL(file);\r\n        }\r\n\r\n        function sendDataToServer(imageData) {\r\n" +
            "fetch('/return_img', {\r\n        method: 'POST',\r\n        headers: {\r\n            'Content-Type': 'application/json'\r\n        },\r\n        body: JSON.stringify({ image: imageData })\r\n    })\r\n    " +
            "  .then(response => {\r\n    return response.blob();\r\n})\r\n.then(blob => {\r\n    const url = URL.createObjectURL(blob);\r\n    const img = document.createElement('img');\r\n    img.src = url;\r\nimg.style.maxWidth = '600px';\r\nimg.style.maxHeight = '400px';\r\n    document.body.appendChild(img);\r\n})\r\n.catch(error => {\r\n    console.error('Ошибка получения изображения:', error);\r\n});\r\n        }\r\n    </script>\r\n</body>\r\n</html>";

        private static string POST_REQUEST = "<html>\r\n<head>\r\n<meta charset=\"utf-8\"/>\r\n<title>PSP-lab5</title>\r\n</head>\r\n  " +
            "<body>\r\n" +
            "<p>\r\n<img height=\"50pd\" width=\"50px\">\r\n</p>\r\n" +
            "<br>\r\n" +
            "</body>\r\n</html>";

        private static string ERROR_PAGE = "<html>\r\n<head>\r\n<meta charset=\"utf-8\"/>\r\n<title>PSP-lab5</title>\r\n</head>\r\n  " +
            "<body>\r\n" +
            "<p>\r\n<label>Page not found</label>\r\n</p>\r\n" +
            "</body>\r\n</html>";

        private static string SERVER_URI = "http://localhost:8080/";
        private static string SERVER_URI_SECURITY = "https://localhost:8080/";

        private bool _isListener = false;

        public HTTPServer()
        {
            _httpServer.Prefixes.Add(SERVER_URI);
        }

        private void SetCorsHeaders(HttpListenerResponse response)
        {
            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
        }

        private void OptionsResponse(HttpListenerContext context)
        {
            HttpListenerResponse response = context.Response;
            SetCorsHeaders(response);
            response.StatusCode = 204;
            response.Close();
        }

        public async void Listener()
        {
            while (_isListener)
            {
                try
                {
                    HttpListenerContext context = await _httpServer.GetContextAsync();

                    new Thread(() =>
                    {
                        try
                        {
                            HttpListenerRequest request = context.Request;

                            if (request != null)
                            {
                                if (request.HttpMethod == "OPTIONS")
                                {
                                    OptionsResponse(context);
                                }
                                else if (request.HttpMethod == "GET")
                                {
                                    GetResponse(context, GET_REQUEST);
                                }
                                else if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/return_img")
                                {
                                    PostResponse(context);
                                }
                                else
                                {
                                    ErrorResponse(context);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing request: {ex.Message}");
                            ErrorResponse(context);
                        }
                    }).Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting context: {ex.Message}");
                }
            }
        }

        private void GetResponse(HttpListenerContext context, string htmlFilePath)
        {
            HttpListenerResponse response = context.Response;
            SetCorsHeaders(response);


            string html = File.ReadAllText(@"F:\Work\7 sem\РИС\KursachHTTP\index.html"); 
            byte[] buffer = Encoding.UTF8.GetBytes(html);

            response.StatusCode = 200;
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.ContentEncoding = Encoding.UTF8;

            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }

        private void PostResponse(object context)
        {
            HttpListenerResponse response = ((HttpListenerContext)context).Response;
            HttpListenerRequest request = ((HttpListenerContext)context).Request;

            SetCorsHeaders(response);

            try
            {
                using (StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    string body = reader.ReadToEnd();

                    string mode = Regex.Match(body, @"""mode"":\s*""(?<mode>.*?)""").Groups["mode"].Value;
                    string imageData = Regex.Match(body, @"""image"":\s*""(?<image>.*?)""").Groups["image"].Value;

                    string base64Data = Regex.Match(imageData, @"data:image/.+;base64,(?<data>.+)").Groups["data"].Value;
                    byte[] imageBytes = ConvertBase64ToBytes(base64Data);

                    try
                    {
                        using (MemoryStream memoryStream = new MemoryStream(imageBytes))
                        {
                            using (Bitmap image = new Bitmap(memoryStream))
                            {
                                if (image.Width <= 0 || image.Height <= 0)
                                {
                                    throw new InvalidDataException("Некорректные данные изображения.");
                                }

                                Bitmap processedImage = (mode == "parallel")
                                    ? _roberts.GetResultParallel(image)
                                    : _roberts.GetResultLinear(image);

                                Console.WriteLine($"{(mode == "parallel" ? "Параллельная" : "Линейная")} обработка выполнена.");
                                SendImageFromBitmapResponse((HttpListenerContext)context, processedImage);
                            }
                        }
                    }
                    catch (InvalidDataException ex)
                    {
                        Console.WriteLine($"Ошибка обработки изображения: {ex.Message}");
                        response.StatusCode = 422; // Unprocessable Entity
                        response.Close();
                        return;
                    }

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in PostResponse: {ex.Message}");
                response.StatusCode = 500;
                response.Close();
            }
        }

        public byte[] ConvertBase64ToBytes(string data)
        {
            try
            {
                string base64Image = Regex.Replace(data, "[^A-Za-z0-9+/=]", "");
                return Convert.FromBase64String(base64Image);
            }
            catch (FormatException ex)
            {
                throw new InvalidDataException("Ошибка декодирования Base64.", ex);
            }
        }


        public byte[][] ConvertBase64ToPixelArray(string base64Image)
        {
            byte[] imageBytes = ConvertBase64ToBytes(base64Image);

            int channels = 4;

            int pixelsCount = imageBytes.Length / channels;

            byte[][] pixelArray = new byte[pixelsCount][];

            for (int i = 0; i < pixelsCount; i++)
            {
                pixelArray[i] = new byte[channels];
                for (int j = 0; j < channels; j++)
                {
                    pixelArray[i][j] = imageBytes[i * channels + j];
                }
            }
            imageBytes = null;

            return pixelArray;
        }

        /*private void SendImageFromByteResponse(HttpListenerContext context, byte[] bitmap, ImageFormat format)
        {
            if (format == ImageFormat.Jpeg)
            {
                context.Response.ContentType = "image/jpeg";
            }
            else
            {
                context.Response.ContentType = "image/png";
            }

            context.Response.ContentLength64 = bitmap.Length;
            context.Response.StatusCode = 200;

            context.Response.OutputStream.Write(bitmap, 0, bitmap.Length);

            context.Response.Close();
        }*/

        private async Task SendImageFromBitmapResponse(HttpListenerContext context, Bitmap bitmap)
        {
            try
            {
                SetCorsHeaders(context.Response);
                context.Response.ContentType = "image/png";
                context.Response.StatusCode = 200;

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    bitmap.Save(memoryStream, ImageFormat.Png);
                    memoryStream.Position = 0;

                    await memoryStream.CopyToAsync(context.Response.OutputStream);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки изображения: {ex.Message}");
                context.Response.StatusCode = 500; // Internal Server Error
            }
            finally
            {
                context.Response.OutputStream.Close();
                context.Response.Close();
            }
        }


        private void ErrorResponse(HttpListenerContext context)
        {
            HttpListenerResponse response = context.Response;
            byte[] buffer = Encoding.UTF8.GetBytes(ERROR_PAGE);

            response.StatusCode = 404;
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.ContentEncoding = Encoding.UTF8;

            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }

        public int Start()
        {
            try
            {
                _httpServer.Start();
                _isListener = true;
                return 1;
            }
            catch
            {
                return 0;
            }

        }

        public int Stop()
        {
            try
            {
                _httpServer.Stop();
                _isListener = false;
                return 1;
            }
            catch
            {
                return 0;
            }
        }

        public int Close()
        {
            try
            {
                _httpServer.Close();
                return 1;
            }
            catch
            {
                return 0;
            }
        }
    }
}
