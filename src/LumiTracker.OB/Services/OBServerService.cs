using LumiTracker.Config;
using LumiTracker.OB;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LumiTracker.Services
{
    public class OBServerService
    {
        private readonly object _clientLock = new object();
        private readonly ConcurrentDictionary<string, TcpClient> _connectedClients = new();

        public async Task StartAsync()
        {
            int port = OBConfig.Root.Get<int>("port");
            string localIp = GetLocalIpAddress();
            Configuration.Logger.LogInformation($"Server started at IP: {localIp}, Port: {port}");

            TcpListener listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Configuration.Logger.LogInformation($"Listening for connections on port {port}...");

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();

                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            string clientId = "";
            try
            {
                // Enable TCP Keep-Alive
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                using NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];

                // Read the client ID
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                clientId = Encoding.UTF8.GetString(buffer, 0, bytesRead).TrimEnd('\0');
                Configuration.Logger.LogInformation($"Received ID: {clientId}");

                // Check if the client ID is valid
                bool isNewClient = false;
                lock (_clientLock)
                {
                    if (!_connectedClients.ContainsKey(clientId))
                    {
                        // Add the client to the dictionary
                        _connectedClients[clientId] = client;
                        isNewClient = true;
                    }
                }

                if (isNewClient)
                {
                    // Connection accepted
                    await stream.WriteAsync(new byte[] { 1 }, 0, 1); // Send acceptance response
                    Configuration.Logger.LogInformation("Connection accepted.");
                    // Handle further communication with the client
                    _connectedClients[clientId] = client;
                }
                else
                {
                    // Connection rejected
                    await stream.WriteAsync(new byte[] { 0 }, 0, 1); // Send rejection response
                    throw new Exception("Connection rejected, client id conflict");
                }
                Configuration.Logger.LogInformation($"Client {clientId} connected.");

                while (client.Connected)
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break; // Client disconnected

                    string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    var message = JsonConvert.DeserializeObject<OBMessage>(json);
                    Configuration.Logger.LogInformation($"Received from client {clientId}: {message.Data}");
                }
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"Client {clientId} error: {ex.Message}");
            }
            finally
            {
                client.Close();
                Configuration.Logger.LogInformation($"Client {clientId} disconnected.");
            }
        }

        private string GetLocalIpAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var localIp = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            if (localIp == null)
            {
                Configuration.Logger.LogError("No IPv4 address found.");
                return "";
            }
            {
                return localIp.ToString();
            }
        }
    }
}