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

            InitializeComponent();

            Resources["TabViewItemHeaderBackground"] = new SolidColorBrush((Color)Application.Current.Resources["SubtleFillColorTertiary"]);
            Resources["TabViewItemHeaderBackgroundSelected"] = (SolidColorBrush)Resources["MainContentBackground"];
            Resources["TabViewSelectedItemBorderBrush"] = (SolidColorBrush)Resources["DeckPageBorderBrush"];
            Resources["TabViewForeground"] = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
        }

        private void OnCardsListClicked(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }
    }
}
