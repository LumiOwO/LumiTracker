using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LumiTracker.Services
{
    public class OBServerService
    {
        private const int PORT = 25251;
        private readonly ConcurrentDictionary<string, TcpClient> _connectedClients = new();

        public async Task StartAsync()
        {
            string localIp = GetLocalIpAddress();
            Console.WriteLine($"Server started at IP: {localIp}, Port: {PORT}");

            TcpListener listener = new TcpListener(IPAddress.Any, PORT);
            listener.Start();
            Console.WriteLine($"Listening for connections on port {PORT}...");

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                string clientId = Guid.NewGuid().ToString(); // Assign a unique ID
                _connectedClients[clientId] = client;

                Console.WriteLine($"Client {clientId} connected.");
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            // Enable TCP Keep-Alive
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            using NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];

            try
            {
                while (client.Connected)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break; // Client disconnected

                    string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    var message = JsonConvert.DeserializeObject<OBMessage>(json);
                    Console.WriteLine($"Received from client: {message.Data}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Client error: {ex.Message}");
            }
            finally
            {
                client.Close();
                Console.WriteLine("Client disconnected.");
            }
        }


        private string GetLocalIpAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var localIp = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            if (localIp == null) throw new Exception("No IPv4 address found.");
            return localIp.ToString();
        }

    }
}