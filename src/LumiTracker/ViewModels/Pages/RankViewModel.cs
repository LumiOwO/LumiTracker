using Wpf.Ui.Controls;
using LumiTracker.Config;
using Microsoft.Extensions.Logging;

namespace LumiTracker.ViewModels.Pages
{
    public partial class RankViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;

        [ObservableProperty]
        private Visibility _webViewVisibility = Visibility.Hidden;
        [ObservableProperty]
        private Visibility _loadingVisibility = Visibility.Visible;

        private void InitializeViewModel()
        {
            LoadingVisibility = Visibility.Visible;
            WebViewVisibility = Visibility.Visible;
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
                LoadingVisibility = Visibility.Visible;
                ShowWebViewDelayed().WaitAsync(TimeSpan.FromMinutes(1));
            }
        }

        public void OnNavigatedFrom() 
        {
            WebViewVisibility = Visibility.Hidden;
        }

        public async Task ShowWebViewDelayed()
        {
            await Task.Delay(500);
            WebViewVisibility = Visibility.Visible;
            LoadingVisibility = Visibility.Hidden;
        }
    }
}
