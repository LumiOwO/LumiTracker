using LumiTracker.ViewModels.Windows;
using LumiTracker.Models;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace LumiTracker.Controls
{
    /// <summary>
    /// Interaction logic for PlayedCardsTabItem.xaml
    /// </summary>
    public partial class CardListTab : UserControl
    {
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
            "ViewModel", typeof(DeckWindowViewModel), typeof(CardListTab), new PropertyMetadata(null));

        public DeckWindowViewModel ViewModel
        {
            get { return (GetValue(ViewModelProperty) as DeckWindowViewModel)!; }
            set { SetValue(ViewModelProperty, value); }
        }

        public static readonly DependencyProperty CardListProperty = DependencyProperty.Register(
            "CardList", typeof(CardList), typeof(CardListTab), new PropertyMetadata(null));

        public CardList CardList
        {
            get { return (GetValue(CardListProperty) as CardList)!; }
            set { SetValue(CardListProperty, value); }
        }

        public static readonly DependencyProperty WindowHeightProperty = DependencyProperty.Register(
            "WindowHeight", typeof(double), typeof(CardListTab), new PropertyMetadata(null));

        public double WindowHeight
        {
            get { return (double)GetValue(WindowHeightProperty); }
            set { SetValue(WindowHeightProperty, value); }
        }

        public static readonly DependencyProperty WindowWidthProperty = DependencyProperty.Register(
            "WindowWidth", typeof(double), typeof(CardListTab), new PropertyMetadata(null));

        public double WindowWidth
        {
            get { return (double)GetValue(WindowWidthProperty); }
            set { SetValue(WindowWidthProperty, value); }
        }

        public CardListTab()
        {
            InitializeComponent();
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            ViewModel.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            ViewModel.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        }

        private void OnChecked(object sender, RoutedEventArgs e)
        {
            ViewModel.ToggleButtonIcon = SymbolRegular.ChevronDown48;
        }

        private void OnUnchecked(object sender, RoutedEventArgs e)
        {
            ViewModel.ToggleButtonIcon = SymbolRegular.ChevronUp48;
        }
    }
}
