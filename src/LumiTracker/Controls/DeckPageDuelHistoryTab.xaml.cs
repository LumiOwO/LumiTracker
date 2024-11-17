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
            "Stats", typeof(BuildStats), typeof(DeckPageDuelHistoryTab), new PropertyMetadata(null));

        public BuildStats Stats
        {
            get { return (BuildStats)GetValue(StatsProperty); }
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

        public static readonly DependencyProperty IncludeAllBuildVersionsProperty = DependencyProperty.Register(
            "IncludeAllBuildVersions", typeof(bool), typeof(DeckPageDuelHistoryTab), new PropertyMetadata(false));

        public bool IncludeAllBuildVersions
        {
            get { return (bool)GetValue(IncludeAllBuildVersionsProperty); }
            set { SetValue(IncludeAllBuildVersionsProperty, value); }
        }

        public static readonly DependencyProperty HideRecordsBeforeImportProperty = DependencyProperty.Register(
            "HideRecordsBeforeImport", typeof(bool), typeof(DeckPageDuelHistoryTab), new PropertyMetadata(false));

        public bool HideRecordsBeforeImport
        {
            get { return (bool)GetValue(HideRecordsBeforeImportProperty); }
            set { SetValue(HideRecordsBeforeImportProperty, value); }
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
        private void DisableListViewSelection(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView)
            {
                listView.SelectedIndex = -1;
            }
        }
    }
}
