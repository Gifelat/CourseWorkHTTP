using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class LoadTester
{
    private const string ServerUrl = "http://localhost:8080/return_img";
    private const int NumberOfRequests = 200;
    private const int MaxDegreeOfParallelism = 20;
    private static readonly string InputFolder = "InputImages"; // Папка с входными изображениями
    private static readonly string OutputFolder = Path.Combine("OutputImages", DateTime.Now.ToString("yyyyMMdd_HHmmss")); // Уникальная папка для сохранения

    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting load test...");
        var client = new HttpClient();

        // Получаем список изображений из папки
        if (!Directory.Exists(InputFolder))
        {
            Console.WriteLine($"Input folder '{InputFolder}' does not exist.");
            return;
        }

        var imagePaths = Directory.GetFiles(InputFolder, "*.*", SearchOption.TopDirectoryOnly);
        if (imagePaths.Length == 0)
        {
            Console.WriteLine($"No images found in folder '{InputFolder}'.");
            return;
        }

        Console.WriteLine($"Found {imagePaths.Length} images in '{InputFolder}'.");

        // Создание уникальной папки для текущего запуска
        Directory.CreateDirectory(OutputFolder);

        var tasks = new List<Task>();
        var stopwatch = Stopwatch.StartNew();
        int successCount = 0;
        int failureCount = 0;

        using (var semaphore = new SemaphoreSlim(MaxDegreeOfParallelism))
        {
            for (int i = 0; i < NumberOfRequests; i++)
            {
                int requestId = i;
                string randomImagePath = imagePaths[new Random().Next(imagePaths.Length)]; 
                await semaphore.WaitAsync();

                tasks.Add(ProcessRequestAsync(client, semaphore, randomImagePath, requestId,
                    incrementSuccessAction: () => Interlocked.Increment(ref successCount),
                    incrementFailureAction: () => Interlocked.Increment(ref failureCount)));
            }

            await Task.WhenAll(tasks);
        }

        stopwatch.Stop();

        Console.WriteLine($"Load test completed in {stopwatch.Elapsed.TotalSeconds} seconds.");
        Console.WriteLine($"Successful requests: {successCount}");
        Console.WriteLine($"Failed requests: {failureCount}");
    }

    private static async Task ProcessRequestAsync(
        HttpClient client,
        SemaphoreSlim semaphore,
        string imagePath,
        int requestId,
        Action incrementSuccessAction,
        Action incrementFailureAction)
    {
        try
        {
            string imageBase64 = Convert.ToBase64String(File.ReadAllBytes(imagePath));

            var content = new StringContent(
                $"{{\"image\":\"data:image/png;base64,{imageBase64}\",\"mode\":\"parallel\"}}",
                Encoding.UTF8,
                "application/json"
            );

            var response = await client.PostAsync(ServerUrl, content);

            if (response.IsSuccessStatusCode)
            {
                incrementSuccessAction();

                // Сохранение изображения
                var responseBytes = await response.Content.ReadAsByteArrayAsync();
                string outputPath = Path.Combine(OutputFolder, $"image_{requestId}.png");
                File.WriteAllBytes(outputPath, responseBytes);
            }
            else
            {
                incrementFailureAction();
                Console.WriteLine($"Request failed: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            incrementFailureAction();
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            semaphore.Release();
        }
    }
}
