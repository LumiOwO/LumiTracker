using LumiTracker.ViewModels.Pages;
using Wpf.Ui.Controls;


namespace LumiTracker.Views.Pages
{
    public partial class RankPage : INavigableView<RankViewModel>
    {
        public RankViewModel ViewModel { get; }

        public RankPage(RankViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }

        private async void WebView_Loaded(object sender, RoutedEventArgs e)
        {
            await RankWebView.EnsureCoreWebView2Async(null);
            //await RankWebView.CoreWebView2.ExecuteScriptAsync(@"
            //    var style = document.createElement('style');
            //    style.innerHTML = 'body, html { margin: 0; padding: 0; width: 100%; overflow-x: scroll; }';
            //    document.head.appendChild(style);
            //");
        }
    }
}
