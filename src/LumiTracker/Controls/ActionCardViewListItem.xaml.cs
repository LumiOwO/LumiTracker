using LumiTracker.Models;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace LumiTracker.Controls
{
    /// <summary>
    /// Interaction logic for PlayedCardsTabItem.xaml
    /// </summary>
    public partial class ActionCardViewListItem : UserControl
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            "Value", typeof(ActionCardView), typeof(ActionCardViewListItem), new PropertyMetadata(null));

        public ActionCardView Value
        {
            get { return (GetValue(ValueProperty) as ActionCardView)!; }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
            "ItemWidth", typeof(double), typeof(ActionCardViewListItem), new PropertyMetadata(null));

        public double ItemWidth
        {
            get { return (double)GetValue(ItemWidthProperty); }
            set { SetValue(ItemWidthProperty, value); }
        }

        public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
            "ItemHeight", typeof(double), typeof(ActionCardViewListItem), new PropertyMetadata(null));

        public double ItemHeight
        {
            get { return (double)GetValue(ItemHeightProperty); }
            set { SetValue(ItemHeightProperty, value); }
        }

        public static readonly DependencyProperty CostFontSizeProperty = DependencyProperty.Register(
            "CostFontSize", typeof(double), typeof(ActionCardViewListItem), new PropertyMetadata(null));

        public double CostFontSize
        {
            get { return (double)GetValue(CostFontSizeProperty); }
            set { SetValue(CostFontSizeProperty, value); }
        }

        public static readonly DependencyProperty NameFontSizeProperty = DependencyProperty.Register(
            "NameFontSize", typeof(double), typeof(ActionCardViewListItem), new PropertyMetadata(null));

        public double NameFontSize
        {
            get { return (double)GetValue(NameFontSizeProperty); }
            set { SetValue(NameFontSizeProperty, value); }
        }

        public static readonly DependencyProperty CountVisibilityProperty = DependencyProperty.Register(
            "CountVisibility", typeof(Visibility), typeof(ActionCardViewListItem), new PropertyMetadata(null));

        public Visibility CountVisibility
        {
            get { return (Visibility)GetValue(CountVisibilityProperty); }
            set { SetValue(CountVisibilityProperty, value); }
        }

        public static readonly DependencyProperty CountFontSizeProperty = DependencyProperty.Register(
            "CountFontSize", typeof(double), typeof(ActionCardViewListItem), new PropertyMetadata(null));

        public double CountFontSize
        {
            get { return (double)GetValue(CountFontSizeProperty); }
            set { SetValue(CountFontSizeProperty, value); }
        }

        public static readonly DependencyProperty SnapshotMarginProperty = DependencyProperty.Register(
            "SnapshotMargin", typeof(Thickness), typeof(ActionCardViewListItem), new PropertyMetadata(new Thickness(0)));

        public Thickness SnapshotMargin
        {
            get { return (Thickness)GetValue(SnapshotMarginProperty); }
            set { SetValue(SnapshotMarginProperty, value); }
        }

        public ActionCardViewListItem()
        {
            InitializeComponent();
        }
    }
}
