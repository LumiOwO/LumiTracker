using LumiTracker.Config;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LumiTracker.Services
{
    public class OBMessage
    {
        public string ClientId { get; set; }
        public string TaskType { get; set; }
        public object? Data { get; set; }
    }

    public class OBClientService
    {
        private readonly string _serverIp;
        private readonly string _clientId;
        private TcpClient? _client = null;
        private NetworkStream? _stream = null;

        public OBClientService(string serverIp)
        {
            _serverIp = serverIp;
            // Use a persistent GUID strategy to identify the client
            _clientId = GetOrCreateClientId();
        }

        private string GetOrCreateClientId()
        {
            string clientIdFilePath = Path.Combine(Configuration.DocumentsDir, "GUID");
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
                Configuration.Logger.LogInformation($"Connecting to {_serverIp}:{port}...");

                await _client.ConnectAsync(_serverIp, port);
                _stream = _client.GetStream();
                _stream.ReadTimeout = 5000; // Set to 5 seconds
                // Enable TCP Keep-Alive
                _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                // Send the client ID
                byte[] idBytes = Encoding.UTF8.GetBytes(_clientId);
                await _stream.WriteAsync(idBytes, 0, idBytes.Length);

                // Wait for server response
                byte[] responseBuffer = new byte[1];
                await _stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
                if (responseBuffer[0] != 1) // Server accepted the ID
                {
                    Configuration.Logger.LogError("Connection rejected.");
                    _client.Close();
                }

                Configuration.Logger.LogInformation("Connected to server.");
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"Connection failed: {ex.Message}");
                _client?.Close();
            }
        }

        // Send a message to the server
        public async Task SendMessageAsync(ETaskType taskType, object? message_data)
        {
            if (_client?.Connected != true)
            {
                Configuration.Logger.LogError("Not connected to server.");
                return;
            }

            try
            {
                var message = new OBMessage 
                { 
                    ClientId = _clientId,
                    TaskType = taskType.ToString(),
                    Data = message_data,
                };
                string json = JsonConvert.SerializeObject(message);
                byte[] data = Encoding.UTF8.GetBytes(json);

                await _stream.WriteAsync(data, 0, data.Length);
                Configuration.Logger.LogInformation("Message sent to server.");
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"Error sending message: {ex.Message}");
            }
        }

        // Close the connection
        public void Close()
        {
            try
            {
                _stream?.Close();
                _client?.Close();
                Configuration.Logger.LogInformation("Connection closed.");
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"Error closing connection: {ex.Message}");
            }
        }
    }
}