using LumiTracker.ViewModels.Windows;
using LumiTracker.Models;
using System.Windows.Controls;
using System.Collections.ObjectModel;

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

        public static readonly DependencyProperty CardListsProperty = DependencyProperty.Register(
            "CardLists", typeof(ObservableCollection<CardList>), typeof(DeckWindowCardListTab), new PropertyMetadata(new ObservableCollection<CardList>()));

        public ObservableCollection<CardList> CardLists
        {
            get { return (GetValue(CardListsProperty) as ObservableCollection<CardList>)!; }
            set { SetValue(CardListsProperty, value); }
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

        public static readonly DependencyProperty NumberToTouchProperty = DependencyProperty.Register(
            "NumberToTouch", typeof(int), typeof(DeckWindowCardListTab), new PropertyMetadata(0));

        public int NumberToTouch
        {
            get { return (int)GetValue(NumberToTouchProperty); }
            set { SetValue(NumberToTouchProperty, value); }
        }

        public DeckWindowCardListTab()
        {
            InitializeComponent();
        }

        private void DisableListViewSelection(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView)
            {
                listView.SelectedIndex = -1;
            }
        }

        [RelayCommand]
        private void OnMouseEnter(CardList list)
        {
            list.ScrollBarVisibility = ScrollBarVisibility.Auto;
        }

        [RelayCommand]
        private void OnMouseLeave(CardList list)
        {
            list.ScrollBarVisibility = ScrollBarVisibility.Hidden;
        }

        public void OnIsExpandedChanged(object sender, RoutedPropertyChangedEventArgs<bool> e)
        {
            NumberToTouch = (NumberToTouch + 1) & 0xf;
        }
    }
}
