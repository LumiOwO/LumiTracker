using Wpf.Ui.Controls;
using LumiTracker.Config;

namespace LumiTracker.ViewModels.Pages
{
    public partial class AboutViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;

        [ObservableProperty]
        private string _appVersion = "";

        public void OnNavigatedTo()
        {
            if (!_isInitialized)
            {
                InitializeViewModel();
            }
        }

        public void OnNavigatedFrom() { }

        private void InitializeViewModel()
        {
            AppVersion = $"{Lang.AppName} v{Configuration.GetAssemblyVersion()}";
            _isInitialized = true;
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
    }
}
