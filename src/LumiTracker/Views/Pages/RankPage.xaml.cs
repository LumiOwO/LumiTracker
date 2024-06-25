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
    }
}
