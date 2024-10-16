using LumiTracker.Config;
using LumiTracker.OB;
using LumiTracker.Watcher;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Net;
using System.IO;
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
                Stream?.Close();
                Stream = null;
                Tcp?.Close();
                Tcp = null;
                ScopeGuard?.Dispose();
                ScopeGuard = null;
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"Error closing client.\n{ex.ToString()}");
            }
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

                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                Configuration.Logger.LogInformation($"Server started at IP: {localIp}, Port: {port}");

                while (true)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client));
                }
            }
            catch (SocketException)
            {
                Configuration.Logger.LogInformation("Server closed, exit connection loop.");
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"Server error.\n{ex.ToString()}");
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
                lock (_clientLock)
                { 
                    foreach (var client in _connectedClients.Values)
                    {
                        client.Close();
                    }
                }
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
                    throw new ArgumentException("Client's Guid is invalid.");
                }
                Configuration.Logger.LogInformation($"Client is trying to connect, Guid: {clientId}");

                // Check if the client ID is valid
                client = GetOrCreateClient(clientId, guidStr, tcp, stream);
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
                            throw new Exception($"Failed to deserialize GameEventMessage");
                        }
                        Configuration.Logger.LogInformation($"{LogHelper.AnsiMagenta}@{LogHelper.AnsiEnd} {json}");
                        client.Proxy!.ParseGameEventTask(message);
                    }
                    catch (Exception ex)
                    {
                        Configuration.Logger.LogError($"Error when receiving message: {ex.Message}\n{json}");
                    }
                }
            }
            catch (IOException)
            {
                Configuration.Logger.LogInformation($"Client disconnected.");
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"Unexpected error.\n{ex.ToString()}");
            }
            finally
            {
                CloseClient(client, clientId);
            }
        }

        private OBClient? GetOrCreateClient(Guid clientId, string guidStr, TcpClient tcp, NetworkStream stream)
        {
            lock (_clientLock)
            {
                if (!_connectedClients.TryGetValue(clientId, out OBClient? client))
                {
                    client = new OBClient();
                    _connectedClients[clientId] = client;
                }

                if (client.Tcp != null)
                {
                    // Already connected
                    return null;
                }

                // Begin log scope here
                if (client.Scope.Guid == Guid.Empty)
                {
                    client.Scope.Guid  = clientId;
                    client.Scope.Name  = guidStr.Substring(0, 4);
                    client.Scope.Color = LogHelper.GetAnsiColorFromGuid(clientId);
                }
                client.ScopeGuard = Configuration.Logger.BeginScope(client.Scope);
                client.Tcp        = tcp;
                client.Stream     = stream;

                if (client.Proxy == null)
                {
                    Configuration.Logger.LogInformation($"Client connected.");
                    client.Proxy = new OBGameWatcherProxy(client.Scope);
                }
                else
                {
                    Configuration.Logger.LogInformation($"Client reconnected.");
                }
                return client;
            }
        }

        private void CloseClient(OBClient? client, Guid clientId)
        {
            if (client == null || clientId == Guid.Empty) return;
            lock (_clientLock)
            {
                if (!_connectedClients.TryGetValue(clientId, out client)) return;
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