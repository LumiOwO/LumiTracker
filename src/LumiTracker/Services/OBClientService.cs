using LumiTracker.Config;
using LumiTracker.Watcher;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace LumiTracker.Services
{
    public class OBClientService
    {
        private readonly string _serverIp;
        private readonly string _clientId;
        private TcpClient? _client = null;
        private NetworkStream? _stream = null;

        private Task? _checkTask = null;
        private CancellationTokenSource? _checkCts = null;

        public OBClientService(string serverIp)
        {
            _serverIp = serverIp;
            // Use a persistent GUID strategy to identify the client
            _clientId = GetOrCreateClientId();
        }

        private string GetOrCreateClientId()
        {
            string clientIdFilePath = Path.Combine(Configuration.OBWorkingDir, "GUID");
            if (File.Exists(clientIdFilePath))
            {
                // Load existing client ID
                return File.ReadAllText(clientIdFilePath);
            }
            else
            {
                // Create a new GUID as client ID and save it
                string newClientId = Guid.NewGuid().ToString();
                File.WriteAllText(clientIdFilePath, newClientId);
                return newClientId;
            }
        }

        // Connect to the server
        public async Task ConnectAsync()
        {
            try
            {
                _client = new TcpClient();
                int port = Configuration.Get<int>("ob_port");
                Configuration.Logger.LogInformation($"[OBClientService] Connecting to {_serverIp}:{port}...");

                await _client.ConnectAsync(_serverIp, port);
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
                    _client.Close();
                }

                _checkTask = CheckConnectionStatusAsync();

                Configuration.Logger.LogInformation("[OBClientService] Connected to server.");
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"[OBClientService] Connection failed.\n{ex.ToString()}");
                _client?.Close();
            }
        }

        public async Task CheckConnectionStatusAsync()
        {
            _checkCts = new CancellationTokenSource();
            CancellationToken token = _checkCts.Token;

            try
            {
                byte[] buffer = new byte[256]; // Adjust buffer size based on expected messages

                while (_client?.Connected == true && !token.IsCancellationRequested)
                {
                    // Read from the stream to detect shutdown signal or disconnection
                    int bytesRead = await _stream!.ReadAsync(buffer, 0, buffer.Length, token);

                    if (bytesRead == 0)
                    {
                        // Server closed the connection gracefully
                        Configuration.Logger.LogInformation("[OBClientService] Server closed the connection.");
                        Close();  // Gracefully close the connection
                        break;
                    }

                    // Add a small delay between checks to avoid tight looping
                    await Task.Delay(500, token);
                }
            }
            catch (OperationCanceledException)
            {
                Configuration.Logger.LogInformation("[OBClientService] Connection canceled.");
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
                _checkTask = null;
                _checkCts  = null;
            }
        }

        // Send a message to the server
        public async Task SendMessageAsync(GameEventMessage message)
        {
            if (_client?.Connected != true)
            {
                Configuration.Logger.LogError("[OBClientService] Not connected to server.");
                return;
            }

            try
            {
                string json = JsonConvert.SerializeObject(message);
                byte[] data = Encoding.UTF8.GetBytes(json);

                await _stream!.WriteAsync(data, 0, data.Length);
                Configuration.Logger.LogDebug("[OBClientService] Message sent to server.");
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"[OBClientService] Error sending message: {ex.Message}");
            }
        }

        // Close the connection
        public void Close()
        {
            try
            {
                _checkCts?.Cancel();
                _stream?.Close();
                _client?.Close();
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"[OBClientService] Error closing connection.\n{ex.ToString()}");
            }
        }
    }
}