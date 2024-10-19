using LumiTracker.Config;
using Microsoft.Extensions.Logging;
using LumiTracker.OB.Services;
using Swordfish.NET.Collections;

namespace LumiTracker.OB.ViewModels.Pages
{
    public partial class ClientInfo : ObservableObject
    {
        [ObservableProperty]
        private bool _connected = true;

        [ObservableProperty]
        private string _name = "123";

        [ObservableProperty]
        private bool _gameStarted = true;

        [ObservableProperty]
        private string _guid = "56645c47-6100-4a68-bf90-7478708f5506";
    }

    public partial class StartViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _serverStarted = false;

        [ObservableProperty]
        private string _natId = "";

        [ObservableProperty]
        private int _port = 25251;

        [ObservableProperty]
        private ConcurrentObservableDictionary<Guid, ClientInfo> _clientInfos = new ();

        private OBServerService _obServerService;
        public StartViewModel(OBServerService obServerService)
        {
            _obServerService = obServerService;
        }

        public void Init()
        {
            Task.Run(_obServerService.StartAsync);
            // TODO: fix server close
            //server?.Close();
            ClientInfos.Add(Guid.NewGuid(), new());
            ClientInfos.Add(Guid.NewGuid(), new());
            ClientInfos.Add(Guid.NewGuid(), new());
        }
    }
}
