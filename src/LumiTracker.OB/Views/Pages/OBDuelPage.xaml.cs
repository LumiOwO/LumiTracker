using LumiTracker.OB.ViewModels.Pages;
using System.Windows.Input;
using Wpf.Ui.Controls;


namespace LumiTracker.OB.Views.Pages
{
    public partial class DuelPage : INavigableView<DuelViewModel>
    {
        public DuelViewModel ViewModel { get; }

        public DuelPage(DuelViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }

        private void OnCardsListClicked(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }
    }
}
