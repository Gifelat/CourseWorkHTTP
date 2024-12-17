using System;

namespace lab5
{
    internal class Program
    {
        private static HTTPServer _server;

        static void Main(string[] args)
        {
            _server = new HTTPServer();

            int status = _server.Start();
            Console.WriteLine(Environment.ProcessorCount);
            if (status == 1) Console.WriteLine("Server is running...");
            else Console.WriteLine("Server startup error...");

            _server.Listener();

            Console.ReadKey();
        }
    }
}
