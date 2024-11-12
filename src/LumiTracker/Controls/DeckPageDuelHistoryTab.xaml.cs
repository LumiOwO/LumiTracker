using LumiTracker.Models;
using System.Windows.Controls;

namespace LumiTracker.Controls
{
    /// <summary>
    /// Interaction logic for DeckPageDuelHistoryTab.xaml
    /// </summary>
    public partial class DeckPageDuelHistoryTab : UserControl
    {
        public static readonly DependencyProperty StatsProperty = DependencyProperty.Register(
            "Stats", typeof(DeckStatistics), typeof(DeckPageDuelHistoryTab), new PropertyMetadata(null));

        public DeckStatistics Stats
        {
            get { return (DeckStatistics)GetValue(StatsProperty); }
            set { SetValue(StatsProperty, value); }
        }

        public static readonly DependencyProperty TabControlHeightProperty = DependencyProperty.Register(
            "TabControlHeight", typeof(double), typeof(DeckPageDuelHistoryTab), new PropertyMetadata(1.0));

        public double TabControlHeight
        {
            get { return (double)GetValue(TabControlHeightProperty); }
            set { SetValue(TabControlHeightProperty, value); }
        }

        public static readonly DependencyProperty TabControlWidthProperty = DependencyProperty.Register(
            "TabControlWidth", typeof(double), typeof(DeckPageDuelHistoryTab), new PropertyMetadata(1.0));

        public double TabControlWidth
        {
            get { return (double)GetValue(TabControlWidthProperty); }
            set { SetValue(TabControlWidthProperty, value); }
        }

        public DeckPageDuelHistoryTab()
        {
            InitializeComponent();
        }
    }
}
