using LumiTracker.Services;
using LumiTracker.ViewModels.Pages;
using Wpf.Ui.Controls;

namespace LumiTracker.Views.Pages
{
    public partial class AboutPage : INavigableView<AboutViewModel>
    {
        public AboutViewModel ViewModel { get; }

        public AboutPage(AboutViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }

        [RelayCommand]
        public async Task OnShowDonateDialog()
        {
            var service = App.GetService<StyledContentDialogService>();
            if (service != null)
            {
                await service.ShowDonateDialogAsync();
            }
        }
    }
}
