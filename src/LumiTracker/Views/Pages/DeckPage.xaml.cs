using LumiTracker.ViewModels.Pages;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;


namespace LumiTracker.Views.Pages
{
    public partial class DeckPage : INavigableView<DeckViewModel>
    {
        public DeckViewModel ViewModel { get; }

        private bool Inited { get; set; } = false;

        private bool IsBuildVersionSelectedByCode { get; set; } = false;

        public DeckPage(DeckViewModel viewModel)
        {
            Loaded += DeckPage_Loaded;

            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
            ViewModel.SelectedCurrentVersionIndexChanged += OnSelectedCurrentVersionIndexChanged;

            Resources["TabViewItemHeaderBackground"] = new SolidColorBrush((Color)Application.Current.Resources["SubtleFillColorTertiary"]);
            Resources["TabViewItemHeaderBackgroundSelected"] = (SolidColorBrush)Resources["MainContentBackground"];
            Resources["TabViewSelectedItemBorderBrush"] = (SolidColorBrush)Resources["DeckPageBorderBrush"];
            Resources["TabViewForeground"] = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));

            Inited = true;
        }

        private void DeckPage_Loaded(object sender, RoutedEventArgs e)
        {
            var chevron = BuildVersionSelectionComboBox.Template.FindName("ChevronIcon", BuildVersionSelectionComboBox) as SymbolIcon;
            if (chevron != null)
            {
                chevron.Margin = new Thickness(0, 0, 0, 0);
                var parentGrid = chevron.Parent as Grid;
                if (parentGrid != null)
                {
                    parentGrid.Margin = new Thickness(0, 0, 8, 0);
                }
            }
        }

        private void DisableListViewSelection(object sender, SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ListView listView)
            {
                listView.SelectedIndex = -1;
            }
        }

        private void OnSelectedCurrentVersionIndexChanged(DeckItem? SelectedDeckItem)
        {
            IsBuildVersionSelectedByCode = true;
            if (SelectedDeckItem != null)
            {
                BuildVersionSelectionComboBox.SelectedIndex = SelectedDeckItem.Info.CurrentVersionIndex ?? 0;
            }
            else
            {
                BuildVersionSelectionComboBox.SelectedIndex = -1;
            }
            IsBuildVersionSelectedByCode = false;
        }

        private void OnCurrentVersionIndexChanged(object sender, SelectionChangedEventArgs e)
        {
            // Only valid when user selected
            if (IsBuildVersionSelectedByCode) return;

            // InitializeComponent() will trigger this
            if (!Inited) 
            {
                // DeckViewModel will be inited when startup, so ViewModel has already loaded when this triggered
                OnSelectedCurrentVersionIndexChanged(ViewModel.SelectedDeckItem);
                return;
            }

            // Note 1: This SelectionChanged trigger will be called immediately when the selection is changed! This is great !!!
            //
            // Note 2: Changing ItemSource will also trigger this...
            // we only want to call this function when user changed the build version from ComboBox,
            // and not trigger when the deck item is changed.
            if (ViewModel.IsChangingDeckItem) return;

            ViewModel.OnCurrentVersionIndexChanged(BuildVersionSelectionComboBox.SelectedIndex);
        }
    }
}
