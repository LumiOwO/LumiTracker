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

        private string GetAssemblyVersion()
        {
            Version? version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "";
        }

        private void InitializeViewModel()
        {
            AppVersion = $"{LocalizationSource.Instance["AppName"]} v{GetAssemblyVersion()}";
            _isInitialized = true;
        }
    }
}
