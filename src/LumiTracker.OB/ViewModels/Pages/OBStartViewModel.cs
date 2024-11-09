using LumiTracker.Config;
using Microsoft.Extensions.Logging;
using LumiTracker.OB.Services;
using Swordfish.NET.Collections;
using System.IO;
using Wpf.Ui.Controls;
using Wpf.Ui;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace LumiTracker.OB.ViewModels.Pages
{
    public partial class ClientInfo : ObservableObject
    {
        [ObservableProperty]
        private bool _connected = false;

        [ObservableProperty]
        private string _name = "";

        [ObservableProperty]
        private bool _gameStarted = false;

        [ObservableProperty]
        private string _guid = "";

        public StartViewModel ViewModel { get; }

        public ClientInfo(StartViewModel viewModel, string name, string guid)
        {
            ViewModel = viewModel;
            _name = name;
            _guid = guid;
        }

        partial void OnNameChanged(string? oldValue, string newValue)
        {
            string dir = Path.Combine(Configuration.OBWorkingDir, Guid);
            if (!Directory.Exists(dir)) return;

            string metaPath = Path.Combine(dir, "meta.json");
            var meta = File.Exists(metaPath) ? Configuration.LoadJObject(metaPath) : new JObject();
            meta["name"] = Name;
            Configuration.SaveJObject(meta, metaPath);
        }
    }

    public partial class StartViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _serverStarted = true;

        [ObservableProperty]
        private string _natId = Configuration.Get<string>("nat_id");

        [ObservableProperty]
        private int _port = Configuration.Get<int>("port");

        [ObservableProperty]
        private string _tokensIconDir = Configuration.Get<string>("tokens_dir");

        [ObservableProperty]
        private int _maxTrackedTokens = Configuration.Get<int>("max_tracked_tokens");

        [ObservableProperty]
        private ConcurrentObservableDictionary<Guid, ClientInfo> _clientInfos = new ();

        private OBServerService _obServerService;
        private ISnackbarService _snackbarService;
        public StartViewModel(OBServerService obServerService, ISnackbarService snackbarService)
        {
            _obServerService = obServerService;
            _snackbarService = snackbarService;
        }

        public void Init()
        {
            Task.Run(() => _obServerService.StartAsync(this));
            // TODO: fix server close
            //server?.Close();
        }

        partial void OnNatIdChanged(string? oldValue, string newValue)
        {
            Configuration.Set("nat_id", newValue);
        }

        partial void OnPortChanged(int oldValue, int newValue)
        {
            Configuration.Set("port", newValue);
        }

        partial void OnTokensIconDirChanged(string? oldValue, string newValue)
        {
            Configuration.Set("tokens_dir", newValue);
        }

        partial void OnMaxTrackedTokensChanged(int oldValue, int newValue)
        {
            Configuration.Set("max_tracked_tokens", newValue);
        }

        [RelayCommand]
        private void OnServerHostAddressCopied()
        {
            string host = "127.0.0.1";
            Clipboard.SetText(host);
            _snackbarService?.Show(
                Lang.OB_HostCopiedToClipboard,
                $"{host}",
                ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.CheckmarkCircle24),
                TimeSpan.FromSeconds(2)
            );
        }

        [RelayCommand]
        public void OnSelectTokensIconDir()
        {
            string initDir = TokensIconDir;
            if (!Directory.Exists(initDir))
            {
                initDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            // OpenFolderDialog requires .NET 8 or newer
            OpenFolderDialog openFolderDialog = new()
            {
                Multiselect = false,
                InitialDirectory = initDir
            };
            if (openFolderDialog.ShowDialog() != true)
            {
                return;
            }
            if (openFolderDialog.FolderNames.Length == 0)
            {
                return;
            }

            TokensIconDir = openFolderDialog.FolderName;
        }

        [RelayCommand]
        public async Task OnLocateLogFile()
        {
            await Configuration.RevealInExplorerAsync(Configuration.LogFilePath);
        }

        [RelayCommand]
        public async Task OnLocateAppDir()
        {
            await Configuration.RevealInExplorerAsync(Configuration.AppDir);
        }

        [RelayCommand]
        private async Task OnOpenClientDataDir(string guid)
        {
            string dir = Path.Combine(Configuration.OBWorkingDir, guid);
            await Configuration.RevealInExplorerAsync(dir);
        }
    }
}
