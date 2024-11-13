using LumiTracker.Models;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Shapes;

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

        public static readonly DependencyProperty SettingsPanelOpenProperty = DependencyProperty.Register(
            "SettingsPanelOpen", typeof(bool), typeof(DeckPageDuelHistoryTab), new PropertyMetadata(false));

        public bool SettingsPanelOpen
        {
            get { return (bool)GetValue(SettingsPanelOpenProperty); }
            set { SetValue(SettingsPanelOpenProperty, value); }
        }

        [RelayCommand]
        private void OnSettingsButtonClick()
        {
            if (!SettingsPanelOpen)
            {
                SettingsPanelOpen = true;
            }
        }

        private void OnMatchupStatsPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject? source = e.OriginalSource as DependencyObject;
            if (source is Image || source is Ellipse || source is Run)
            {
                e.Handled = true;
            }
            else if (source is FrameworkElement element && element.DataContext is MatchupStats)
            {
                e.Handled = true;
            }
            else
            {
                e.Handled = false;
            }
        }

        private void OnDuelRecordPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject? source = e.OriginalSource as DependencyObject;
            if (source is Image || source is Ellipse || source is Run)
            {
                e.Handled = true;
            }
            else if (source is FrameworkElement element && element.DataContext is DuelRecord)
            {
                e.Handled = true;
            }
            else
            {
                e.Handled = false;
            }
        }
    }
}
