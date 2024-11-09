using LumiTracker.Config;
using LumiTracker.Watcher;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Net;
using System.IO;
using System.Net.Sockets;
using System.Text;
using LumiTracker.OB.ViewModels.Pages;
using Newtonsoft.Json.Linq;

namespace LumiTracker.OB.Services
{
    public class OBClient
    {
        public OBGameWatcherProxy? Proxy      { get; set; } = null;
        public TcpClient?          Tcp        { get; set; } = null;
        public NetworkStream?      Stream     { get; set; } = null;
        public IDisposable?        ScopeGuard { get; set; } = null;
        public ScopeState          Scope      { get; set; } = new ();
        public ClientInfo?         Info       { get; set; } = null;

        public void Close()
        {
            try
            {
                if (Stream != null)
                {
                    Stream.Flush();
                    Stream.Close();
                    Stream = null;
                }
                if (Tcp != null)
                {
                    Tcp.Close();
                    Tcp = null;
                }
                if (ScopeGuard != null)
                {
                    ScopeGuard.Dispose();
                    ScopeGuard = null;
                }
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
        private StartViewModel? ViewModel = null;

        public OBGameWatcherProxy? GetGameWatcherProxy(Guid guid)
        {
            OBClient? client = null;
            _connectedClients.TryGetValue(guid, out client);
            return client?.Proxy;
        }

        public async Task StartAsync(StartViewModel viewModel)
        {
            ViewModel = viewModel;
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

                string messageBuffer = "";
                while (tcp.Connected)
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        Configuration.Logger.LogInformation("Client closed the connection.");
                        break;
                    }

                    messageBuffer += Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    int endIdx;
                    while ((endIdx = messageBuffer.IndexOf('\n')) != -1)
                    {
                        try
                        {
                            // Extract the message from the buffer
                            string json = messageBuffer.Substring(0, endIdx);
                            messageBuffer = messageBuffer.Substring(endIdx + 1); // Remove processed message

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
                            Configuration.Logger.LogError($"Error when receiving message: {ex.Message}\nCurrent buffer: {messageBuffer}");
                            // Clear the messageBuffer if failed, so it may process later messages
                            messageBuffer = "";
                        }
                    }
                }
            }
            catch (IOException)
            {
                Configuration.Logger.LogInformation($"Disconnected from client.");
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

                // Begin log scope
                if (client.Scope.Guid == Guid.Empty)
                {
                    client.Scope.Guid  = clientId;
                    client.Scope.Name  = guidStr.Substring(0, 4);
                    client.Scope.Color = LogHelper.GetAnsiColorFromGuid(clientId);
                }
                client.ScopeGuard = Configuration.Logger.BeginScope(client.Scope);
                client.Tcp        = tcp;
                client.Stream     = stream;

                // Process client info
                string dataPath = Path.Combine(Configuration.OBWorkingDir, guidStr);
                if (!Directory.Exists(dataPath))
                {
                    Directory.CreateDirectory(dataPath);
                }
                if (!ViewModel!.ClientInfos.TryGetValue(clientId, out ClientInfo? clientInfo))
                {
                    string clientName = Lang.Player + guidStr.Substring(0, 4);
                    string metaPath = Path.Combine(dataPath, "meta.json");
                    if (File.Exists(metaPath))
                    {
                        var meta = Configuration.LoadJObject(metaPath);
                        if (meta.TryGetValue("name", out JToken? nameToken) && nameToken.Type == JTokenType.String)
                        {
                            clientName = nameToken.ToString();
                        }
                    }
                    clientInfo = new ClientInfo(ViewModel!, clientName, guidStr);
                    ViewModel!.ClientInfos[clientId] = clientInfo;
                }
                client.Info = clientInfo;

                // Start GameWatcher proxy
                if (client.Proxy == null)
                {
                    Configuration.Logger.LogInformation($"Client connected.");
                    client.Proxy = new OBGameWatcherProxy(client.Scope, client.Info);
                }
                else
                {
                    Configuration.Logger.LogInformation($"Client reconnected.");
                }
                client.Info.Connected = true;
                return client;
            }
        }

        private void CloseClient(OBClient? client, Guid clientId)
        {
            if (client == null || clientId == Guid.Empty) return;
            lock (_clientLock)
            {
                if (!_connectedClients.TryGetValue(clientId, out client)) return;
                client.Close();
                client.Info!.Connected = false;
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