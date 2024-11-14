using Wpf.Ui.Controls;
using LumiTracker.Config;
using Microsoft.Extensions.Logging;
using LumiTracker.Models;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.IO;
using System.Windows.Input;
using LumiTracker.Services;
using System.Windows.Data;
using Wpf.Ui;

namespace LumiTracker.ViewModels.Pages
{
    enum EControlButtonType : int
    {
        //SetAsActiveDeck,
        EditDeckName,
        ReimportDeck,
        ShareDeck,
        SelectedDeckOperationLast = ShareDeck,

        AddNewDeck,
        DeleteDeck,

        NumControlButtons
    }

    public partial class ControlButton : ObservableObject
    {
        public LocalizationTextItem TextItem { get; }
        public SymbolRegular Icon { get; }
        public ICommand ClickCommand { get; }
        public ControlAppearance Appearance { get; }

        [ObservableProperty]
        private bool _isEnabled = true;
        [ObservableProperty]
        private double _opacity = 1.0;
        partial void OnIsEnabledChanged(bool oldValue, bool newValue)
        {
            Opacity = IsEnabled ? 1.0 : 0.3;
        }

        public ControlButton(string textKey, SymbolRegular icon, ICommand command, ControlAppearance appearance, bool isEnabled)
        {
            TextItem = new LocalizationTextItem();
            var binding = LocalizationExtension.Create(textKey);
            BindingOperations.SetBinding(TextItem, LocalizationTextItem.TextProperty, binding);

            Icon         = icon;
            ClickCommand = command;
            Appearance   = appearance;
            IsEnabled    = isEnabled;
        }
    }

    public partial class DeckStatistics : ObservableObject
    {
        private BuildStats Total { get; set; }

        private ObservableCollection<BuildStats> AllBuildStats { get; set; }

        /////////////////////////
        // Current
        [ObservableProperty]
        private BuildStats _current;

        public DeckStatistics()
        {
            Total = new();
            AllBuildStats = [new(), new(), new(), new()];
            Current = Total;
        }
    }

    public partial class DeckItem(DeckInfo deckInfo) : ObservableObject
    {
        [ObservableProperty]
        private DeckInfo _info = deckInfo;

        [ObservableProperty]
        private ObservableCollection<ActionCardView> _actionCards = [];

        [ObservableProperty]
        private DeckStatistics? _stats = new ();

        [ObservableProperty]
        private FontWeight _weightInNameList = FontWeights.Light;

        [ObservableProperty]
        private Brush? _colorInNameList = (SolidColorBrush?)Application.Current?.Resources?["TextFillColorPrimaryBrush"];

        public void OnSelected()
        {
            if (ActionCards.Count == 0)
            {
                ///////////////////////
                // Decode share code
                ImportFromShareCode(Info.ShareCode);
            }

            // TODO: Init DeckStatistics
        }

        public bool ImportFromShareCode(string sharecode)
        {
            int[]? cards = DeckUtils.DecodeShareCode(sharecode);
            if (cards == null)
            {
                Configuration.Logger.LogWarning($"[DeckItem.DecodeShareCode] Invalid share code: {sharecode}");
                return false;
            }
            Info.ShareCode = sharecode;
            Info.Characters = cards[..3].ToList();

            ///////////////////////
            // Current Deck

            // Sort in case of non-standard order
            Array.Sort(cards, 3, 30,
                Comparer<int>.Create((x, y) => DeckUtils.ActionCardCompare(x, y)));

            // To column major
            ActionCardView[] views = new ActionCardView[30];
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 15; y++)
                {
                    // transpose
                    int srcIdx = 3 + x * 15 + y;
                    int dstIdx = y * 2 + x;
                    views[dstIdx] = new ActionCardView(null, cards[srcIdx], inGame: false);
                }
            }

            ActionCards = new ObservableCollection<ActionCardView>(views);
            return true;
        }
    }

    public partial class DeckViewModel : ObservableObject, INavigationAware
    {
        [ObservableProperty]
        private ObservableCollection<DeckItem> _deckItems = [];

        [ObservableProperty]
        private int _selectedIndex = -1;

        [ObservableProperty]
        private int _activeDeckIndex = -1;

        /////////////////////////
        // UI
        [ObservableProperty]
        private DeckItem? _selectedDeckItem = null;

        [ObservableProperty]
        private ControlButton[] _buttons = new ControlButton[(int)EControlButtonType.NumControlButtons];

        private ISnackbarService? SnackbarService;

        private StyledContentDialogService? ContentDialogService;

        private static readonly string UserDecksDescPath = Path.Combine(
            Configuration.ConfigDir,
            "decks.json"
        );

        public DeckViewModel(ISnackbarService? snackbarService, StyledContentDialogService? contentDialogService)
        {
            /////////////////////////
            // Services
            SnackbarService = snackbarService;
            ContentDialogService = contentDialogService;

            /////////////////////////
            // Init buttons
            //Buttons[(int)EControlButtonType.SetAsActiveDeck] = new ControlButton(
            //    textKey    : "SetAsActiveDeck",
            //    icon       : SymbolRegular.Checkmark24,
            //    command    : SetAsActiveDeckClickedCommand,
            //    appearance : ControlAppearance.Caution,
            //    isEnabled  : false
            //);
            Buttons[(int)EControlButtonType.EditDeckName] = new ControlButton(
                textKey    : "EditDeckName",
                icon       : SymbolRegular.Pen24,
                command    : EditDeckNameClickedCommand,
                appearance : ControlAppearance.Secondary,
                isEnabled  : false
            );
            Buttons[(int)EControlButtonType.ReimportDeck] = new ControlButton(
                textKey    : "ReimportDeck",
                icon       : SymbolRegular.ArrowSync24,
                command    : ReimportDeckClickedCommand,
                appearance : ControlAppearance.Secondary,
                isEnabled  : false
            );
            Buttons[(int)EControlButtonType.ShareDeck] = new ControlButton(
                textKey    : "ShareDeck",
                icon       : SymbolRegular.Share24,
                command    : ShareDeckClickedCommand,
                appearance : ControlAppearance.Secondary,
                isEnabled  : false
            );
            Buttons[(int)EControlButtonType.AddNewDeck] = new ControlButton(
                textKey    : "AddNewDeck",
                icon       : SymbolRegular.AddCircle24,
                command    : AddNewDeckClickedCommand,
                appearance : ControlAppearance.Secondary,
                isEnabled  : true
            );
            Buttons[(int)EControlButtonType.DeleteDeck] = new ControlButton(
                textKey    : "DeleteDeck",
                icon       : SymbolRegular.Delete24,
                command    : DeleteDeckClickedCommand,
                appearance : ControlAppearance.Danger,
                isEnabled  : true
            );

            /////////////////////////
            // TODO: Load deck infos
            //if (!File.Exists(UserDecksDescPath))
            //{
            //    return;
            //}
            //var userDecksDesc = Configuration.LoadJObject(UserDecksDescPath);
            //var userDecks = userDecksDesc.ToObject<DeckInformations>();
            //if (userDecks == null)
            //{
            //    Configuration.Logger.LogWarning($"Failed to load user decks.");
            //    return;
            //}
            //DeckInformations.DeckInfos   = userDecks.DeckInfos;
            //DeckInformations.ActiveIndex = Math.Clamp(userDecks.ActiveIndex, -1, DeckInformations.DeckInfos.Count - 1);
            //SelectedDeckIndex = DeckInformations.ActiveIndex;
        }

        partial void OnSelectedIndexChanged(int oldValue, int newValue)
        {
            Select(newValue);
        }

        public static readonly SolidColorBrush HighlightColor = new SolidColorBrush(Color.FromArgb(0xff, 0xf9, 0xca, 0x24));
        partial void OnActiveDeckIndexChanged(int oldValue, int newValue)
        {
            if (oldValue >= 0 && oldValue < DeckItems.Count)
            {
                DeckItems[oldValue].WeightInNameList = FontWeights.Light;
                DeckItems[oldValue].ColorInNameList  = (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"];
            }
            if (newValue >= 0 && newValue < DeckItems.Count)
            {
                DeckItems[newValue].WeightInNameList = FontWeights.Bold;
                DeckItems[newValue].ColorInNameList  = HighlightColor;
            }
        }

        private void Select(int index)
        {
            ///////////////////////
            // Check index
            bool valid = (index >= 0);
            for (EControlButtonType i = 0; i <= EControlButtonType.SelectedDeckOperationLast; i++)
            {
                Buttons[(int)i].IsEnabled = valid;
            }

            if (valid)
            {
                Configuration.Logger.LogDebug($"Select deck[{index}], {DeckItems.Count} decks in total");
                DeckItems[index].OnSelected();
                SelectedDeckItem = DeckItems[index];
            }
            else
            {
                SelectedDeckItem = null;
            }
        }

        public void SaveDeckList()
        {
            // TODO: Save deck infos
            //Configuration.SaveJObject(JObject.FromObject(UserDeckList), UserDecksDescPath);
        }

        [RelayCommand]
        private void OnSetAsActiveDeckClicked()
        {
            Configuration.Logger.LogDebug("OnSetAsActiveDeckClicked");
            if (SelectedIndex < 0 || SelectedDeckItem == null)
            {
                return;
            }

            ActiveDeckIndex = SelectedIndex;
            SaveDeckList();
            SnackbarService?.Show(
                Lang.SetAsActiveSuccess_Title,
                Lang.SetAsActiveSuccess_Message + SelectedDeckItem.Info.Name,
                ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.CheckmarkCircle24),
                TimeSpan.FromSeconds(2)
            );
        }

        [RelayCommand]
        private async Task OnEditDeckNameClicked()
        {
            Configuration.Logger.LogDebug("OnEditDeckNameClicked");
            if (SelectedIndex < 0 || SelectedDeckItem == null || ContentDialogService == null)
            {
                return;
            }

            var (result, name) = await ContentDialogService.ShowTextInputDialogAsync(
                Lang.EditDeckName_Title,
                SelectedDeckItem.Info.Name,
                Lang.EditDeckName_Placeholder
            );

            if (result == ContentDialogResult.Primary)
            {
                if (name == "")
                {
                    name = Lang.DefaultDeckName;
                }
                SelectedDeckItem.Info.Name = name;
                SaveDeckList();
            }
        }

        [RelayCommand]
        private async Task OnReimportDeckClicked()
        {
            Configuration.Logger.LogDebug("OnReimportDeckClicked");
            if (SelectedIndex < 0 || SelectedDeckItem == null || ContentDialogService == null)
            {
                return;
            }

            var (result, sharecode) = await ContentDialogService.ShowTextInputDialogAsync(
                Lang.ImportDeck_Title,
                "",
                Lang.ImportDeck_Placeholder
            );

            if (result == ContentDialogResult.Primary)
            {
                bool success = SelectedDeckItem.ImportFromShareCode(sharecode);
                if (success)
                {
                    SelectedDeckItem.Info.LastModified = DateTime.Now;
                    SaveDeckList();
                }
                else
                {
                    SnackbarService?.Show(
                        Lang.InvalidSharingCode_Title,
                        Lang.InvalidSharingCode_Message + sharecode,
                        ControlAppearance.Danger,
                        new SymbolIcon(SymbolRegular.DismissCircle24),
                        TimeSpan.FromSeconds(3)
                    );
                }
            }
        }

        [RelayCommand]
        private void OnShareDeckClicked()
        {
            Configuration.Logger.LogDebug("OnShareDeckClicked");
            if (SelectedIndex < 0 || SelectedDeckItem == null)
            {
                return;
            }

            string sharecode = SelectedDeckItem.Info.ShareCode;
            Clipboard.SetText(sharecode);
            SnackbarService?.Show(
                Lang.CopyToClipboard,
                $"{sharecode}",
                ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.CheckmarkCircle24),
                TimeSpan.FromSeconds(2)
            );
        }

        [RelayCommand]
        private async Task OnAddNewDeckClicked()
        {
            Configuration.Logger.LogDebug("OnAddNewDeckClicked");
            if (ContentDialogService == null)
            {
                return;
            }

            var (result, sharecode) = await ContentDialogService.ShowTextInputDialogAsync(
                Lang.ImportDeck_Title, 
                "",
                Lang.ImportDeck_Placeholder
            );

            if (result == ContentDialogResult.Primary)
            {
                int[]? cards = DeckUtils.DecodeShareCode(sharecode);
                if (cards != null)
                {
                    string deckName = "";
                    foreach (int c_id in cards[..3])
                    {
                        if (deckName != "") deckName += "+";
                        deckName += Configuration.GetCharacterName(c_id, is_short: true);
                    }

                    var info = new DeckInfo()
                    {
                        ShareCode    = sharecode,
                        Characters   = cards[..3].ToList(),
                        Name         = deckName,
                        LastModified = DateTime.Now,
                    };
                    DeckItems.Add(new DeckItem(info));
                    SelectedIndex = DeckItems.Count - 1;
                    SaveDeckList();
                }
                else
                {
                    Configuration.Logger.LogWarning($"[OnAddNewDeckClicked] Invalid share code: {sharecode}");
                    SnackbarService?.Show(
                        Lang.InvalidSharingCode_Title,
                        Lang.InvalidSharingCode_Message + sharecode,
                        ControlAppearance.Danger,
                        new SymbolIcon(SymbolRegular.DismissCircle24),
                        TimeSpan.FromSeconds(3)
                    );
                }
            }
        }

        [RelayCommand]
        private async Task OnDeleteDeckClicked()
        {
            Configuration.Logger.LogDebug("OnDeleteDeckClicked");
            if (SelectedIndex < 0 || SelectedDeckItem == null || ContentDialogService == null)
            {
                return;
            }

            var result = await ContentDialogService.ShowDeleteConfirmDialogAsync(SelectedDeckItem.Info.Name);

            if (result == ContentDialogResult.Primary)
            {
                Configuration.Logger.LogDebug($"Before delete: SelectedIndex = {SelectedIndex}, ActiveDeckIndex = {ActiveDeckIndex}");

                int prevSelectedIndex = SelectedIndex;
                DeckItems.RemoveAt(SelectedIndex); // SelectedIndex -> -1

                // Update active index
                if (prevSelectedIndex == ActiveDeckIndex)
                {
                    ActiveDeckIndex = -1;
                }
                else if (prevSelectedIndex < ActiveDeckIndex)
                {
                    ActiveDeckIndex--;
                }
                else // (prevSelectedIndex > ActiveDeckIndex)
                {

                }

                // Restore SelectedIndex
                SelectedIndex = Math.Min(prevSelectedIndex, DeckItems.Count - 1);

                Configuration.Logger.LogDebug($"After delete: SelectedIndex = {SelectedIndex}, ActiveDeckIndex = {ActiveDeckIndex}");

                SaveDeckList();
            }
        }

        public void OnNavigatedTo()
        {
            if (ActiveDeckIndex >= 0 && ActiveDeckIndex < DeckItems.Count)
            {
                SelectedIndex = ActiveDeckIndex;
            }
            else if (SelectedIndex < 0 && DeckItems.Count > 0)
            {
                SelectedIndex = 0;
            }
        }

        public void OnNavigatedFrom() 
        {

        }
    }
}
