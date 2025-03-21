using LumiTracker.Models;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace LumiTracker.Controls
{
    /// <summary>
    /// Interaction logic for PlayedCardsTabItem.xaml
    /// </summary>
    public partial class ActionCardViewListItem : UserControl
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            "Value", typeof(ActionCardView), typeof(ActionCardViewListItem), new PropertyMetadata(null, OnValueChanged));

        public ActionCardView Value
        {
            get { return (GetValue(ValueProperty) as ActionCardView)!; }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
            "ItemWidth", typeof(double), typeof(ActionCardViewListItem), new PropertyMetadata(1.0));

        public double ItemWidth
        {
            get { return (double)GetValue(ItemWidthProperty); }
            set { SetValue(ItemWidthProperty, value); }
        }

        public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
            "ItemHeight", typeof(double), typeof(ActionCardViewListItem), new PropertyMetadata(1.0));

        public double ItemHeight
        {
            get { return (double)GetValue(ItemHeightProperty); }
            set { SetValue(ItemHeightProperty, value); }
        }

        public static readonly DependencyProperty CostFontSizeProperty = DependencyProperty.Register(
            "CostFontSize", typeof(double), typeof(ActionCardViewListItem), new PropertyMetadata(1.0));

        public double CostFontSize
        {
            get { return (double)GetValue(CostFontSizeProperty); }
            set { SetValue(CostFontSizeProperty, value); }
        }

        public static readonly DependencyProperty NameFontSizeProperty = DependencyProperty.Register(
            "NameFontSize", typeof(double), typeof(ActionCardViewListItem), new PropertyMetadata(1.0));

        public double NameFontSize
        {
            get { return (double)GetValue(NameFontSizeProperty); }
            set { SetValue(NameFontSizeProperty, value); }
        }

        public static readonly DependencyProperty HideCountProperty = DependencyProperty.Register(
            "HideCount", typeof(bool), typeof(ActionCardViewListItem), new PropertyMetadata(false));

        public bool HideCount
        {
            get { return (bool)GetValue(HideCountProperty); }
            set { SetValue(HideCountProperty, value); }
        }

        public static readonly DependencyProperty CountFontSizeProperty = DependencyProperty.Register(
            "CountFontSize", typeof(double), typeof(ActionCardViewListItem), new PropertyMetadata(1.0));

        public double CountFontSize
        {
            get { return (double)GetValue(CountFontSizeProperty); }
            set { SetValue(CountFontSizeProperty, value); }
        }

        private Storyboard blinkAnimation;

        public ActionCardViewListItem()
        {
            InitializeComponent();

            blinkAnimation = (Storyboard)FindResource("BlinkAnimation");
            Storyboard.SetTarget(blinkAnimation, HighlightPanel);
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // For unknown reason, OnValueChanged will be called multiple time from null -> value
            if (e.NewValue is ActionCardView newValue && newValue.ShouldNotify())
            {
                var control = (ActionCardViewListItem)d;
                control.blinkAnimation.Begin();
                newValue.MarkAsNotified();
            }
        }
    }
}
