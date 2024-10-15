using LumiTracker.Config;
using LumiTracker.OB;
using LumiTracker.Watcher;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LumiTracker.Services
{
    public class OBClient
    {
        public OBGameWatcherProxy? Proxy      { get; set; } = null;
        public TcpClient?          Tcp        { get; set; } = null;
        public NetworkStream?      Stream     { get; set; } = null;
        public IDisposable?        ScopeGuard { get; set; } = null;
        public ScopeState          Scope      { get; set; } = new ();

        public void Close()
        {
            try
            {
                if (Stream != null)
                {
                    byte[] shutdownMessage = Encoding.UTF8.GetBytes("SHUTDOWN");
                    Stream.Write(shutdownMessage, 0, shutdownMessage.Length);
                    Stream.Flush(); // Ensure the message is sent immediately
                }
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"Error sending shutdown signal: {ex.Message}");
            }

            Stream?.Close();
            Tcp?.Close();
            ScopeGuard?.Dispose();
        }
    }

    public class OBServerService
    {
        private readonly object _clientLock = new object();
        private readonly ConcurrentDictionary<Guid, OBClient> _connectedClients = new ();
        private TcpListener? _listener = null;

        public async Task StartAsync()
        {
            try
            {
                int port = Configuration.Get<int>("port");
                string localIp = GetLocalIpAddress();
                Configuration.Logger.LogInformation($"Server started at IP: {localIp}, Port: {port}");

                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                Configuration.Logger.LogInformation($"Listening for connections on port {port}...");

                while (true)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client));
                }
            }
            catch (ObjectDisposedException)
            {
                Configuration.Logger.LogInformation("Listener stopped. Exiting connection loop.");
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"Server error: {ex.ToString()}");
            }
            finally
            {
                Cleanup();
            }
        }

        public void Close()
        {
            _listener?.Stop();
        }

        private void Cleanup()
        {
            try
            {
                foreach (var client in _connectedClients.Values)
                {
                    client.Close();
                }
                Configuration.Logger.LogInformation("Server closed.");
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"Error closing Server.\n{ex.ToString()}");
            }
        }

        private async Task HandleClientAsync(TcpClient tcp)
        {
            OBClient? client = null;
            Guid clientId = Guid.Empty;
            try
            {
                // Enable TCP Keep-Alive
                tcp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                NetworkStream stream = tcp.GetStream();
                byte[] buffer = new byte[1024];

                // Read the client ID
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                string guidStr = Encoding.UTF8.GetString(buffer, 0, bytesRead).TrimEnd('\0');
                // Parse the GUID string into a Guid object
                if (!Guid.TryParse(guidStr, out clientId))
                {
                    clientId = Guid.Empty;
                    throw new ArgumentException("Invalid GUID format.");
                }
                Configuration.Logger.LogInformation($"Received ID: {clientId}");

                // Check if the client ID is valid
                lock (_clientLock)
                {
                    if (!_connectedClients.ContainsKey(clientId))
                    {
                        client = new OBClient();
                        _connectedClients[clientId] = client;
                    }
                }

                if (client != null)
                {
                    // Connection accepted
                    await stream.WriteAsync([1], 0, 1); // Send acceptance response
                }
                else
                {
                    // Connection rejected
                    await stream.WriteAsync([0], 0, 1); // Send rejection response
                    throw new Exception("Connection rejected: client id conflict or OBClient not created.");
                }

                // Begin log scope here
                client.Scope.Guid  = clientId;
                client.Scope.Name  = guidStr.Substring(0, 4);
                client.Scope.Color = LogHelper.GetAnsiColorFromGuid(clientId);
                client.ScopeGuard  = Configuration.Logger.BeginScope(client.Scope);
                client.Tcp         = tcp;
                client.Stream      = stream;
                client.Proxy       = new OBGameWatcherProxy(client.Scope);
                Configuration.Logger.LogInformation($"Client connected.");

                while (tcp.Connected)
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break; // Client disconnected

                    string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    try
                    {
                        var message = JsonConvert.DeserializeObject<GameEventMessage>(json);
                        if (message == null)
                        {
                            throw new Exception($"Failed to deserialize GameEventMessage: {json}");
                        }
                        Configuration.Logger.LogInformation($"{LogHelper.AnsiMagenta}@{LogHelper.AnsiEnd} {json}");
                        client.Proxy.ParseGameEventTask(message);
                    }
                    catch (Exception ex)
                    {
                        Configuration.Logger.LogError($"Client error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"Client error: {ex.Message}");
            }
            finally
            {
                Configuration.Logger.LogInformation($"Client disconnected.");
                client?.Close();
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