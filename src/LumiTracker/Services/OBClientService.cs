using LumiTracker.Config;
using LumiTracker.Watcher;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace LumiTracker.OB.Services
{
    public delegate void OnServerDisconnectedCallback();

    public class OBClientService
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _clientId;
        private TcpClient? _client = null;
        private NetworkStream? _stream = null;

        public event OnServerDisconnectedCallback? ServerDisconnected;

        private Task? CheckTask = null;

        public OBClientService(string host, int port)
        {
            _host = host;
            _port = port;
            // Use a persistent GUID strategy to identify the client
            _clientId = Configuration.GetOrCreateClientId(out bool isNewlyCreated);
        }

        // Connect to the server
        public async Task<bool> ConnectAsync()
        {
            try
            {
                _client = new TcpClient();
                Configuration.Logger.LogInformation($"[OBClientService] Connecting to {_host}:{_port}...");

                await _client.ConnectAsync(_host, _port);
                _stream = _client.GetStream();
                _stream.ReadTimeout = 5000; // Set to 5 seconds
                // Enable TCP Keep-Alive
                _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                // Send the client ID
                byte[] idBytes = Encoding.UTF8.GetBytes(_clientId);
                await _stream.WriteAsync(idBytes, 0, idBytes.Length);

                // Wait for server response
                byte[] responseBuffer = [0];
                int bytesRead = await _stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
                if (bytesRead == 0 || responseBuffer[0] != 1)
                {
                    Configuration.Logger.LogError("[OBClientService] Connection rejected.");
                    Close();
                    return false;
                }

                CheckTask = CheckConnectionStatusAsync();

                Configuration.Logger.LogInformation("[OBClientService] Connected to server.");
                return true;
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"[OBClientService] Connection failed.\n{ex.ToString()}");
                Close();
                return false;
            }
        }

        public async Task CheckConnectionStatusAsync()
        {
            try
            {
                byte[] buffer = new byte[256]; // Adjust buffer size based on expected messages

                while (_client?.Connected == true)
                {
                    // Read from the stream to detect shutdown signal or disconnection
                    int bytesRead = await _stream!.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        // Server closed the connection gracefully
                        Configuration.Logger.LogInformation("[OBClientService] Server closed the connection.");
                        break;
                    }

                    // Add a small delay between checks to avoid tight looping
                    await Task.Delay(500);
                }
            }
            catch (IOException)
            {
                Configuration.Logger.LogInformation("[OBClientService] Disconnected from server.");
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"[OBClientService] Unexpected error.\n{ex.ToString()}");
            }
            finally
            {
                // In case of exception, need to call Close() here
                // So, for a normal close in OnExit(), Close() will be called twice
                Close(fromCheckTask: true);
            }
        }

        // Send a message to the server
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);  // Initialize with 1 available slot
        public async Task SendMessageAsync(GameEventMessage message)
        {
            if (_client?.Connected != true)
            {
                Configuration.Logger.LogError("[OBClientService] Not connected to server.");
                return;
            }

            await _lock.WaitAsync();  // Asynchronous lock acquisition
            try
            {
                string json = JsonConvert.SerializeObject(message) + "\n";
                byte[] data = Encoding.UTF8.GetBytes(json);

                await _stream!.WriteAsync(data, 0, data.Length);
                // Configuration.Logger.LogDebug($"[OBClientService] Message sent to server: {json}");
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"[OBClientService] Error sending message: {ex.Message}");
            }
            finally
            {
                _lock.Release();  // Release the lock
            }
        }

        public bool Connected()
        {
            return _stream != null && _client != null && _client.Connected;
        }

        // Close the connection
        public void Close(bool fromCheckTask = false)
        {
            try
            {
                if (_stream != null)
                {
                    _stream.Flush();
                    _stream.Close();
                    _stream = null;
                }
                if (_client != null)
                {
                    _client.Close();
                    _client = null;
                    ServerDisconnected?.Invoke();
                    ServerDisconnected = null;
                }
                if (!fromCheckTask)
                {
                    CheckTask?.Wait();
                }
                CheckTask = null;
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"[OBClientService] Error closing connection.\n{ex.ToString()}");
            }
        }
    }
}