using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using System.Text.Json;

namespace Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            int port = GetPortFromSettings(); // Получение порта из настроек

            // Создание и настройка счетчика
            Counter counter = new Counter();

            // Создание и настройка сервера соксетов и сервера веб-соксетов
            TcpListener tcpListener = null;
            HttpListener httpListener = null;

            Console.WriteLine($"Server is listening on port {port}");

            while (true)
            {
                Console.WriteLine("Выберите тип соединения:");
                Console.WriteLine("1. Соксет");
                Console.WriteLine("2. Веб-соксет");

                int choice;
                if (int.TryParse(Console.ReadLine(), out choice) && (choice == 1 || choice == 2))
                {
                    if (choice == 1)
                    {
                        tcpListener = new TcpListener(IPAddress.Any, port);
                        tcpListener.Start();
                        Console.WriteLine("Сервер соксетов запущен.");
                    }
                    else
                    {
                        httpListener = new HttpListener();
                        httpListener.Prefixes.Add($"http://localhost:{port}/");
                        httpListener.Start();
                        Console.WriteLine("Сервер веб-соксетов запущен.");
                    }

                    break;
                }
                else
                {
                    Console.WriteLine("Пожалуйста, выберите корректный тип соединения (1 или 2).");
                }
            }

            while (true)
            {
                if (tcpListener != null)
                {
                    TcpClient tcpClient = await tcpListener.AcceptTcpClientAsync();
                    await Task.Run(() => HandleSocketClientAsync(tcpClient, counter));
                }

                if (httpListener != null)
                {
                    HttpListenerContext context = await httpListener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
                        HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(null);
                        WebSocket webSocket = webSocketContext.WebSocket;
                        await Task.Run(() => HandleWebSocketClientAsync(webSocket, counter));
                    }
                }
            }
        }



        // Метод для обработки клиентского подключения через соксеты
        static async Task HandleSocketClientAsync(TcpClient client, Counter counter)
        {
            Console.WriteLine("Соксет-клиент подключен.");

            using (NetworkStream stream = client.GetStream())
            using (StreamReader reader = new StreamReader(stream))
            using (StreamWriter writer = new StreamWriter(stream))
            {
                try
                {
                    while (true)
                    {
                        string request = await reader.ReadLineAsync();
                        if (string.IsNullOrEmpty(request))
                        {
                            break; // Клиент отключился
                        }

                        if (request.Equals("START", StringComparison.OrdinalIgnoreCase))
                        {
                            counter.Start();
                            writer.WriteLine("Счетчик запущен.");
                            writer.Flush();
                        }
                        else if (request.Equals("STOP", StringComparison.OrdinalIgnoreCase))
                        {
                            counter.Stop();
                            writer.WriteLine("Счетчик остановлен.");
                            writer.Flush();
                        }
                        else if (request.Equals("CONTINUE", StringComparison.OrdinalIgnoreCase))
                        {
                            counter.Start();
                            writer.WriteLine("Счетчик продолжен.");
                            writer.Flush();
                        }
                        else if (request.Equals("RESET", StringComparison.OrdinalIgnoreCase))
                        {
                            counter.Reset();
                            writer.WriteLine("Счетчик сброшен.");
                            writer.Flush();
                        }
                        else if (request.Equals("GET", StringComparison.OrdinalIgnoreCase))
                        {
                            int value = counter.Value;
                            writer.WriteLine($"Значение счетчика: {value}");
                            writer.Flush();
                        }
                        else
                        {
                            writer.WriteLine("Неверная команда. Допустимые команды: START, STOP, CONTINUE, RESET, GET.");
                            writer.Flush();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка в соксет-клиенте: {ex.Message}");
                }
                finally
                {
                    client.Close();
                    Console.WriteLine("Соксет-клиент отключен.");
                }
            }
        }


        static async Task HandleWebSocketClientAsync(WebSocket webSocket, Counter counter)
        {
            Console.WriteLine("Веб-соксет-клиент подключен.");

            try
            {
                byte[] buffer = new byte[1024];

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string request = Encoding.UTF8.GetString(buffer, 0, result.Count);

                        // Десериализация JSON-строки в объект
                        var requestData = System.Text.Json.JsonSerializer.Deserialize<RequestData>(request);

                        // Обработка запроса и отправка ответа
                        string response = ProcessRequest(requestData, counter);

                        // Сериализация ответа в JSON и отправка через веб-соксет
                        var responseData = new ResponseData { Message = response };
                        string jsonResponse = System.Text.Json.JsonSerializer.Serialize(responseData);
                        await SendWebSocketMessageAsync(webSocket, jsonResponse);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в веб-соксет-клиенте: {ex.Message}");
            }
            finally
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Закрыто сервером", CancellationToken.None);
                Console.WriteLine("Веб-соксет-клиент отключен.");
            }
        }

        static int GetPortFromSettings()
        {
            int defaultPort = 8080;
            while (true)
            {
                Console.Write("Введите порт (по умолчанию {0}): ", defaultPort);
                string input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    return defaultPort;
                }

                if (int.TryParse(input, out int port) && port > 0 && port <= 65535)
                {
                    return port;
                }
                else
                {
                    Console.WriteLine("Пожалуйста, введите корректное значение порта (1-65535).");
                }
            }
        }
        static string ProcessRequest(RequestData request, Counter counter)
        {
            switch (request.Command.ToUpper())
            {
                case "START":
                    counter.Start();
                    return "Счетчик запущен.";
                case "STOP":
                    counter.Stop();
                    return "Счетчик остановлен.";
                case "CONTINUE":
                    counter.Start();
                    return "Счетчик продолжен.";
                case "RESET":
                    counter.Reset();
                    return "Счетчик сброшен.";
                case "GET":
                    int value = counter.Value;
                    return $"Значение счетчика: {value}";
                default:
                    return "Неверная команда. Допустимые команды: START, STOP, CONTINUE, RESET, GET.";
            }
        }
        static async Task SendWebSocketMessageAsync(WebSocket webSocket, string message)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
