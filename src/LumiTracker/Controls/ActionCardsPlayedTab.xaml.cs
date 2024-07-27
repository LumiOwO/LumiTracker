using LumiTracker.ViewModels.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Wpf.Ui.Controls;

using CardList = Swordfish.NET.Collections.ConcurrentObservableSortedDictionary<
    int, LumiTracker.ViewModels.Windows.ActionCardView>;

namespace LumiTracker.Controls
{
    /// <summary>
    /// Interaction logic for PlayedCardsTabItem.xaml
    /// </summary>
    public partial class ActionCardsPlayedTab : UserControl
    {
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
            "ViewModel", typeof(DeckWindowViewModel), typeof(ActionCardsPlayedTab), new PropertyMetadata(null));

        public DeckWindowViewModel ViewModel
        {
            get { return (GetValue(ViewModelProperty) as DeckWindowViewModel)!; }
            set { SetValue(ViewModelProperty, value); }
        }

        public static readonly DependencyProperty ActionCardsPlayedProperty = DependencyProperty.Register(
            "ActionCardsPlayed", typeof(CardList), typeof(ActionCardsPlayedTab), new PropertyMetadata(null));

        public CardList ActionCardsPlayed
        {
            get { return (GetValue(ActionCardsPlayedProperty) as CardList)!; }
            set { SetValue(ActionCardsPlayedProperty, value); }
        }

        public static readonly DependencyProperty WindowHeightProperty = DependencyProperty.Register(
            "WindowHeight", typeof(double), typeof(ActionCardsPlayedTab), new PropertyMetadata(null));

        public double WindowHeight
        {
            get { return (double)GetValue(WindowHeightProperty); }
            set { SetValue(WindowHeightProperty, value); }
        }

        public static readonly DependencyProperty WindowWidthProperty = DependencyProperty.Register(
            "WindowWidth", typeof(double), typeof(ActionCardsPlayedTab), new PropertyMetadata(null));

        public double WindowWidth
        {
            get { return (double)GetValue(WindowWidthProperty); }
            set { SetValue(WindowWidthProperty, value); }
        }

        public ActionCardsPlayedTab()
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
