using LumiTracker.ViewModels.Pages;
using System.Windows.Input;
using System.Windows.Media;
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

            // Hide selected color of list view
            this.Resources["ListViewItemPillFillBrush"] = new SolidColorBrush(Color.FromArgb(0x00, 0x00, 0x00, 0x00));

            InitializeComponent();
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }
    }
}
