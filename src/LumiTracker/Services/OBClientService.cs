using LumiTracker.Config;
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
        private const int SERVER_PORT = 25251;
        private readonly string _serverIp;
        private TcpClient _client;
        private NetworkStream _stream;
        private readonly string _clientId;

        public OBClientService(string serverIp)
        {
            _serverIp = serverIp;
            // Use a persistent GUID strategy to identify the client
            _clientId = GetOrCreateClientId();
        }

        private string GetOrCreateClientId()
        {
            const string clientIdFilePath = "client_id.txt"; // Store the client ID locally

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
                Console.WriteLine($"Connecting to {_serverIp}:{SERVER_PORT}...");

                await _client.ConnectAsync(_serverIp, SERVER_PORT);
                _stream = _client.GetStream();

                // Enable TCP Keep-Alive
                _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                Console.WriteLine("Connected to server.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection failed: {ex.Message}");
            }
        }

        // Send a message to the server
        public async Task SendMessageAsync(ETaskType taskType, object? message_data)
        {
            if (_client?.Connected != true)
            {
                Console.WriteLine("Not connected to server.");
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
                Console.WriteLine("Message sent to server.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
            }
        }

        // Close the connection
        public void Close()
        {
            try
            {
                _stream?.Close();
                _client?.Close();
                Console.WriteLine("Connection closed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing connection: {ex.Message}");
            }
        }
    }
}