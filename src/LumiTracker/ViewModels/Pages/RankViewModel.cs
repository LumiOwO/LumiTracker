using Wpf.Ui.Controls;
using LumiTracker.Config;
using Microsoft.Extensions.Logging;

namespace LumiTracker.ViewModels.Pages
{
    public partial class RankViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;

        [ObservableProperty]
        private bool _webViewVisible = false;
        [ObservableProperty]
        private bool _loadingVisible = true;

        private void InitializeViewModel()
        {
            LoadingVisible = true;
            WebViewVisible = true;
            _isInitialized = true;
        }

        public void OnNavigatedTo()
        {
            if (!_isInitialized)
            {
                InitializeViewModel();
            }
            else
            {
                LoadingVisible = true;
                ShowWebViewDelayed().WaitAsync(TimeSpan.FromMinutes(1));
            }
        }

        public void OnNavigatedFrom() 
        {
            WebViewVisible = false;
        }

        public async Task ShowWebViewDelayed()
        {
            await Task.Delay(500);
            WebViewVisible = true;
            LoadingVisible = false;
        }
    }
}
