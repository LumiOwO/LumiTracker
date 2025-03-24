using LumiTracker.Config;
using Newtonsoft.Json.Linq;
using System.Net.Sockets;
using System.Net;
using Windows.Foundation.Metadata;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;

namespace LumiTracker.Watcher
{
    public delegate void OnMessageReceivedCallback(JObject? message, Exception? exception);

    public interface IBackend
    {
        bool Init(CaptureInfo info);

        Task<bool> StartAsync();

        bool HasExited();

        void Kill();

        Task Send(object message_obj);

        event OnMessageReceivedCallback? MessageReceived;
    }

    public enum EBackend : int
    {
        Python,
        CSharp, // TODO: public class CoreSystem
    }

    public class BackendFactory
    {
        public static IBackend Create(EBackend type)
        {
            return type switch
            {
                EBackend.Python => new PythonBackend(),
                EBackend.CSharp => new PythonBackend(),
                _ => new PythonBackend(),
            };
        }
    }

    public class PythonBackend : IBackend
    {
        private readonly string InitFilePath = Path.Combine(Configuration.DocumentsDir, "init.json");

        private readonly bool TestCaptureOnResize = false;

        private Process? process { get; set; } = null;

        private Socket? socket { get; set; } = null;

        private int port { get; set; } = 0;

        private bool Inited { get; set; } = false;

        public event OnMessageReceivedCallback? MessageReceived;

        public bool Init(CaptureInfo info)
        {
            //////////////////////////
            // Prepare start info
            string clientType = info.ClientType.ToString();
            string captureType = EnumHelpers.BitBltUnavailable(info.ClientType) ?
                ECaptureType.WindowsCapture.ToString() :
                info.CaptureType.ToString();

            bool canHideBorder = ApiInformation.IsPropertyPresent(
                "Windows.Graphics.Capture.GraphicsCaptureSession", "IsBorderRequired");

            // Grab available port
            var tempSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tempSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            port = ((IPEndPoint)tempSocket!.LocalEndPoint!).Port;
            tempSocket.Close();

            // Save init parameters to json file
            bool saved = Configuration.SaveJObject(JObject.FromObject(new
            {
                hwnd            = info.hwnd.ToInt64(),
                client_type     = clientType,
                capture_type    = captureType,
                can_hide_border = canHideBorder ? 1 : 0,
                port            = port,
                log_dir         = Configuration.LogDir,
                test_on_resize  = TestCaptureOnResize ? 1 : 0,
            }), InitFilePath);
            if (!saved)
            {
                Configuration.Logger.LogError("[PythonBackend] Failed to save ProcessWatcher init file.");
                return false;
            }

            Inited = true;
            return true;
        }

        public async Task<bool> StartAsync()
        {
            Debug.Assert(Inited);

            var startInfo = new ProcessStartInfo
            {
                FileName  = Path.Combine(Configuration.AppDir, "python", "python.exe"),
                Arguments = $"-E -m watcher.window_watcher \"{InitFilePath}\"",
                UseShellExecute       = false,
                RedirectStandardError = true,
                CreateNoWindow        = true,
                WorkingDirectory      = Configuration.AppDir,
            };

            //////////////////////////
            // Create backend process
            process = new Process();
            process.StartInfo = startInfo;
            process.ErrorDataReceived += (s, e) => 
            {
                try
                {
                    if (e.Data == null) return;
                    JObject message = JObject.Parse(e.Data);
                    MessageReceived?.Invoke(message, null);
                }
                catch (JsonReaderException ex)
                {
                    Configuration.Logger.LogError($"[python] {e.Data}");
                    MessageReceived?.Invoke(null, ex);
                }
                catch (Exception ex)
                {
                    MessageReceived?.Invoke(null, ex);
                }
            };

            if (!process.Start())
            {
                Configuration.Logger.LogError("[PythonBackend] Failed to start subprocess.");
                return false;
            }
            ChildProcessTracker.AddProcess(process);
            process.BeginErrorReadLine();

            //////////////////////////
            // Connect backend socket
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port));
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"[PythonBackend] Failed to connect to backend socket.\n{ex.ToString()}");
                Kill();
                return false;
            }
            Configuration.Logger.LogInformation($"[PythonBackend] Connected to backend socket on port {port}.");
            return true;
        }

        public bool HasExited() => process?.HasExited ?? true;

        public void Kill()
        {
            socket?.Dispose();
            socket = null;
            process?.Kill();
        }

        public async Task Send(object message_obj)
        {
            if (HasExited()) return;

            string message_str = "";
            try
            {
                message_str = LogHelper.JsonToConsoleStr(JObject.FromObject(message_obj), forceCompact: true);
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"[PythonBackend] Failed to send message object. \n{ex.ToString()}");
                return;
            }

            Debug.Assert(socket != null);
            byte[] data = Encoding.UTF8.GetBytes(message_str + "\n");
            await Task.Factory.FromAsync(
                (callback, state) => socket.BeginSend(data, 0, data.Length, SocketFlags.None, callback, state),
                socket.EndSend,
                null);
        }
    }
}
