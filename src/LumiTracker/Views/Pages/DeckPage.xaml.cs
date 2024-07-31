using LumiTracker.ViewModels.Pages;
using System.Windows.Input;
using Wpf.Ui.Controls;


namespace LumiTracker.Views.Pages
{
    public partial class DeckPage : INavigableView<DeckViewModel>
    {
        public DeckViewModel ViewModel { get; }

        public DeckPage(DeckViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }
    }
}
