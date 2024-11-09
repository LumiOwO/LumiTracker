using LumiTracker.ViewModels.Windows;
using LumiTracker.Models;
using System.Windows.Controls;
using System.Windows.Input;

namespace LumiTracker.Controls
{
    /// <summary>
    /// Interaction logic for PlayedCardsTabItem.xaml
    /// </summary>
    public partial class DeckWindowCardListTab : UserControl
    {
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
            "ViewModel", typeof(DeckWindowViewModel), typeof(DeckWindowCardListTab), new PropertyMetadata(null));

        public DeckWindowViewModel ViewModel
        {
            get { return (GetValue(ViewModelProperty) as DeckWindowViewModel)!; }
            set { SetValue(ViewModelProperty, value); }
        }

        public static readonly DependencyProperty CardListProperty = DependencyProperty.Register(
            "CardList", typeof(CardList), typeof(DeckWindowCardListTab), new PropertyMetadata(null));

        public CardList CardList
        {
            get { return (GetValue(CardListProperty) as CardList)!; }
            set { SetValue(CardListProperty, value); }
        }

        public static readonly DependencyProperty WindowHeightProperty = DependencyProperty.Register(
            "WindowHeight", typeof(double), typeof(DeckWindowCardListTab), new PropertyMetadata(1.0));

        public double WindowHeight
        {
            get { return (double)GetValue(WindowHeightProperty); }
            set { SetValue(WindowHeightProperty, value); }
        }

        public static readonly DependencyProperty WindowWidthProperty = DependencyProperty.Register(
            "WindowWidth", typeof(double), typeof(DeckWindowCardListTab), new PropertyMetadata(1.0));

        public double WindowWidth
        {
            get { return (double)GetValue(WindowWidthProperty); }
            set { SetValue(WindowWidthProperty, value); }
        }

        public static readonly DependencyProperty VerticalScrollBarVisibilityProperty = DependencyProperty.Register(
            "VerticalScrollBarVisibility", typeof(ScrollBarVisibility), typeof(DeckWindowCardListTab), new PropertyMetadata(ScrollBarVisibility.Hidden));

        public ScrollBarVisibility VerticalScrollBarVisibility
        {
            get { return (ScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty); }
            set { SetValue(VerticalScrollBarVisibilityProperty, value); }
        }

        public DeckWindowCardListTab()
        {
            InitializeComponent();
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject? source = e.OriginalSource as DependencyObject;
            if (source is FrameworkElement element && element.DataContext is ActionCardViewListItem)
            {
                e.Handled = true;
            }
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        }
    }
}
