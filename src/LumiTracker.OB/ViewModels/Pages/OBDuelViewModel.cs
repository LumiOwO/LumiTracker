using Wpf.Ui.Controls;
using LumiTracker.Config;
using Microsoft.Extensions.Logging;
using LumiTracker.ViewModels.Windows;
using LumiTracker.OB.Services;

namespace LumiTracker.OB.ViewModels.Pages
{
    public partial class DuelViewModel : ObservableObject, INavigationAware
    {
        [ObservableProperty]
        private int _playerNames = -1;

        [ObservableProperty]
        private int _my_SelectedPlayerIndex = -1;

        [ObservableProperty]
        private DeckWindowViewModel? _my_DeckWindowViewModel = null;

        [ObservableProperty]
        private int _op_SelectedPlayerIndex = -1;

        [ObservableProperty]
        private DeckWindowViewModel? _op_DeckWindowViewModel = null;

        [ObservableProperty]
        private StartViewModel _startViewModel;

        private OBServerService _obServerService;

        public DuelViewModel(OBServerService obServerService, StartViewModel startViewModel)
        {
            _obServerService = obServerService;
            _startViewModel = startViewModel;
        }

        public void OnNavigatedFrom()
        {

        }

        public void OnNavigatedTo()
        {
            int numClients = StartViewModel.ClientInfos.Count;
            if (My_SelectedPlayerIndex < 0 && numClients > 0)
            {
                My_SelectedPlayerIndex = 0;
            }
            if (Op_SelectedPlayerIndex < 0 && numClients > 0)
            {
                Op_SelectedPlayerIndex = numClients > 1 ? 1 : 0;
            }
        }

        partial void OnMy_SelectedPlayerIndexChanged(int oldValue, int newValue)
        {
            if (newValue < 0 || newValue >= StartViewModel.ClientInfos.Count) return;

            Guid guid = StartViewModel.ClientInfos.CollectionView[newValue].Key;
            var proxy = _obServerService.GetGameWatcherProxy(guid);
            My_DeckWindowViewModel = proxy?.DeckWindowViewModel;
        }

        partial void OnOp_SelectedPlayerIndexChanged(int oldValue, int newValue)
        {
            if (newValue < 0 || newValue >= StartViewModel.ClientInfos.Count) return;

            Guid guid = StartViewModel.ClientInfos.CollectionView[newValue].Key;
            var proxy = _obServerService.GetGameWatcherProxy(guid);
            Op_DeckWindowViewModel = proxy?.DeckWindowViewModel;
        }
    }
}
