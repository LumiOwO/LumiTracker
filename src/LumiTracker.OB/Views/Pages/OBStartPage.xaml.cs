using LumiTracker.OB.ViewModels.Pages;
using Wpf.Ui.Controls;

namespace LumiTracker.OB.Views.Pages
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
    }
}
