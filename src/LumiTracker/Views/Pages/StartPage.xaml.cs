using LumiTracker.Services;
using LumiTracker.ViewModels.Pages;
using Wpf.Ui.Controls;

namespace LumiTracker.Views.Pages
{
    public partial class StartPage : INavigableView<StartViewModel>
    {
        public StartViewModel ViewModel { get; }

        public StartPage(StartViewModel viewModel)
        {
            ViewModel   = viewModel;
            DataContext = this;

            InitializeComponent();
        }

        public void Init()
        {
            ViewModel.Init();
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
