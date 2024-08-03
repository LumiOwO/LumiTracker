using Wpf.Ui.Controls;
using LumiTracker.Config;
using Microsoft.Extensions.Logging;
using LumiTracker.Models;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using System.Windows.Media;
using System.IO;
using System.Windows.Input;
using LumiTracker.Services;
using System.Windows.Data;
using Newtonsoft.Json.Linq;
using Wpf.Ui;

namespace LumiTracker.ViewModels.Pages
{
    public partial class AvatarView : ObservableObject
    {
        [ObservableProperty]
        public string _avatarUri = "pack://siteoforigin:,,,/assets/images/avatars/0.png";

        [ObservableProperty]
        public Visibility _avatarImageVisibility = Visibility.Hidden;

        public AvatarView() { }

        public AvatarView(int character_id)
        {
            var info = Configuration.Database["characters"]![character_id]!;
            AvatarUri = $"pack://siteoforigin:,,,/assets/images/avatars/{character_id}.png";
            AvatarImageVisibility = Visibility.Visible;
        }
    }

    public partial class DeckInfo : ObservableObject
    {
        [JsonProperty("sharecode")]
        public string ShareCode = "";

        [JsonProperty("name")]
        [ObservableProperty]
        [property: JsonIgnore]
        private string _name = LocalizationSource.Instance["DefaultDeckName"];

        [JsonProperty("characters")]
        public int[] Characters = [];

        [ObservableProperty]
        [property: JsonIgnore]
        private FontWeight _textWeight = FontWeights.Light;

        [ObservableProperty]
        [property: JsonIgnore]
        private Brush _textColor = (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"];
    }

    public partial class DeckList : ObservableObject
    {
        public static readonly SolidColorBrush HighlightColor = new SolidColorBrush(Color.FromArgb(0xff, 0xf9, 0xca, 0x24));

        [JsonProperty("active")]
        [ObservableProperty]
        [property: JsonIgnore]
        private int _activeIndex = -1;

        [JsonProperty("decks")]
        [ObservableProperty]
        [property: JsonIgnore]
        private ObservableCollection<DeckInfo> _deckInfos = [];

        partial void OnActiveIndexChanged(int oldValue, int newValue)
        {
            if (oldValue >= 0 && oldValue < DeckInfos.Count)
            {
                DeckInfos[oldValue].TextWeight = FontWeights.Light;
                DeckInfos[oldValue].TextColor  = (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"];
            }
            if (newValue >= 0)
            {
                DeckInfos[newValue].TextWeight = FontWeights.Bold;
                DeckInfos[newValue].TextColor  = HighlightColor;
            }
        }
    }

    enum EControlButtonType : int
    {
        SetAsActiveDeck,
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

    public partial class DeckViewModel : ObservableObject, INavigationAware
    {
        [ObservableProperty]
        private ObservableCollection<AvatarView> _avatars = [ new AvatarView(), new AvatarView(), new AvatarView() ];

        [ObservableProperty]
        private ObservableCollection<ActionCardView> _currentDeck = [];

        [ObservableProperty]
        private DeckList _userDeckList = new ();

        [ObservableProperty]
        private int _selectedDeckIndex = -1;

        [ObservableProperty]
        private string _selectedDeckName = "";

        partial void OnSelectedDeckIndexChanged(int oldValue, int newValue)
        {
            Select(newValue);
        }

        [ObservableProperty]
        private ControlButton[] _buttons = new ControlButton[(int)EControlButtonType.NumControlButtons];

        private static readonly string UserDecksDescPath = Path.Combine(
            Configuration.ConfigDir,
            "decks.json"
        );

        private ISnackbarService SnackbarService;

        private StyledContentDialogService ContentDialogService;

        public DeckViewModel(ISnackbarService snackbarService, StyledContentDialogService contentDialogService)
        {
            /////////////////////////
            // Services
            SnackbarService = snackbarService;
            ContentDialogService = contentDialogService;

            /////////////////////////
            // Init buttons
            Buttons[(int)EControlButtonType.SetAsActiveDeck] = new ControlButton(
                textKey    : "SetAsActiveDeck",
                icon       : SymbolRegular.Checkmark24,
                command    : SetAsActiveDeckClickedCommand,
                appearance : ControlAppearance.Caution,
                isEnabled  : false
            );
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
            // Load deck infos
            if (!File.Exists(UserDecksDescPath))
            {
                return;
            }
            var userDecksDesc = Configuration.LoadJObject(UserDecksDescPath);
            var userDecks = userDecksDesc.ToObject<DeckList>();
            if (userDecks == null)
            {
                Configuration.Logger.LogWarning($"Failed to load user decks.");
                return;
            }
            UserDeckList.DeckInfos   = userDecks.DeckInfos;
            UserDeckList.ActiveIndex = userDecks.ActiveIndex;
            SelectedDeckIndex        = userDecks.ActiveIndex;
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
            if (!valid)
            {
                Avatars = [new AvatarView(), new AvatarView(), new AvatarView()];
                CurrentDeck = [];
                SelectedDeckName = "";
                return;
            }

            ///////////////////////
            // Decode share code
            DeckInfo info = UserDeckList.DeckInfos[index];
            string sharecode = info.ShareCode;
            DecodeShareCode(info, sharecode);
        }

        private bool DecodeShareCode(DeckInfo info, string sharecode) 
        {
            int[]? cards = DeckUtils.DecodeShareCode(sharecode);
            if (cards == null)
            {
                Configuration.Logger.LogWarning($"Invalid share code: {sharecode}");
                return false;
            }
            info.ShareCode = sharecode;
            SelectedDeckName = info.Name;

            ///////////////////////
            // Avatars
            info.Characters = new int[3];
            for (int i = 0; i < 3; i++)
            {
                info.Characters[i] = cards[i];
                Avatars[i] = new AvatarView(cards[i]);
            }

            ///////////////////////
            // Current Deck

            // Sort in case of non-standard order
            Array.Sort(cards, 3, 30, 
                Comparer<int>.Create((x, y) => DeckUtils.ActionCardCompare(x, y)));

            // To column major
            int[] transposed = new int[30];
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 15; y++)
                {
                    transposed[y * 2 + x] = cards[3 + x * 15 + y];
                }
            }

            ObservableCollection<ActionCardView> currentDeck = new ();
            foreach (int card_id in transposed)
            {
                currentDeck.Add(new ActionCardView(card_id, inGame: false));
            }
            CurrentDeck = currentDeck;

            return true;
        }

        private void SaveDeckList()
        {
            Configuration.SaveJObject(JObject.FromObject(UserDeckList), UserDecksDescPath);
        }


        [RelayCommand]
        private void OnSetAsActiveDeckClicked()
        {
            Configuration.Logger.LogDebug("OnSetAsActiveDeckClicked");
            if (SelectedDeckIndex < 0)
            {
                return;
            }

            UserDeckList.ActiveIndex = SelectedDeckIndex;
            SaveDeckList();
            SnackbarService.Show(
                LocalizationSource.Instance["SetAsActiveSuccess_Title"],
                LocalizationSource.Instance["SetAsActiveSuccess_Message"] + SelectedDeckName,
                ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.CheckmarkCircle24),
                TimeSpan.FromSeconds(2)
            );
        }

        [RelayCommand]
        private async Task OnEditDeckNameClicked()
        {
            Configuration.Logger.LogDebug("OnEditDeckNameClicked");
            if (SelectedDeckIndex < 0)
            {
                return;
            }

            var (result, name) = await ContentDialogService.ShowTextInputDialogAsync(
                LocalizationSource.Instance["EditDeckName_Title"],
                SelectedDeckName,
                LocalizationSource.Instance["EditDeckName_Placeholder"]
            );

            if (result == ContentDialogResult.Primary)
            {
                if (name == "")
                {
                    name = LocalizationSource.Instance["DefaultDeckName"];
                }
                UserDeckList.DeckInfos[SelectedDeckIndex].Name = name;
                SelectedDeckName = name;
                SaveDeckList();
            }
        }

        [RelayCommand]
        private async Task OnReimportDeckClicked()
        {
            Configuration.Logger.LogDebug("OnReimportDeckClicked");
            if (SelectedDeckIndex < 0)
            {
                return;
            }

            var (result, sharecode) = await ContentDialogService.ShowTextInputDialogAsync(
                LocalizationSource.Instance["ImportDeck_Title"],
                "",
                LocalizationSource.Instance["ImportDeck_Placeholder"]
            );

            if (result == ContentDialogResult.Primary)
            {
                var info = UserDeckList.DeckInfos[SelectedDeckIndex];
                bool success = DecodeShareCode(info, sharecode);
                if (success)
                {
                    SaveDeckList();
                }
                else
                {
                    SnackbarService.Show(
                        LocalizationSource.Instance["InvalidSharingCode_Title"],
                        LocalizationSource.Instance["InvalidSharingCode_Message"] + sharecode,
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
            if (SelectedDeckIndex < 0)
            {
                return;
            }

            var info = UserDeckList.DeckInfos[SelectedDeckIndex];
            string sharecode = info.ShareCode;

            Clipboard.SetText(sharecode);
            SnackbarService.Show(
                LocalizationSource.Instance["CopyToClipboard"],
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
            var (result, sharecode) = await ContentDialogService.ShowTextInputDialogAsync(
                LocalizationSource.Instance["ImportDeck_Title"], 
                "",
                LocalizationSource.Instance["ImportDeck_Placeholder"]
            );

            if (result == ContentDialogResult.Primary)
            {
                var info = new DeckInfo();
                bool success = DecodeShareCode(info, sharecode);
                if (success)
                {
                    UserDeckList.DeckInfos.Add(info);
                    SelectedDeckIndex = UserDeckList.DeckInfos.Count - 1;
                    SaveDeckList();
                }
                else
                {
                    SnackbarService.Show(
                        LocalizationSource.Instance["InvalidSharingCode_Title"],
                        LocalizationSource.Instance["InvalidSharingCode_Message"] + sharecode,
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
            if (SelectedDeckIndex < 0)
            {
                return;
            }

            var result = await ContentDialogService.ShowDeleteConfirmDialogAsync(SelectedDeckName);

            if (result == ContentDialogResult.Primary)
            {
                Configuration.Logger.LogDebug($"Before delete: SelectedDeckIndex = {SelectedDeckIndex}, ActiveIndex = {UserDeckList.ActiveIndex}");

                int prevSelectedDeckIndex = SelectedDeckIndex;
                UserDeckList.DeckInfos.RemoveAt(SelectedDeckIndex); // SelectedDeckIndex -> -1

                // Update active index
                if (prevSelectedDeckIndex == UserDeckList.ActiveIndex)
                {
                    UserDeckList.ActiveIndex = -1;
                }
                else if (prevSelectedDeckIndex < UserDeckList.ActiveIndex)
                {
                    UserDeckList.ActiveIndex--;
                }
                else // (SelectedDeckIndex > prevActiveIndex)
                {

                }

                // Restore SelectedDeckIndex
                SelectedDeckIndex = Math.Min(prevSelectedDeckIndex, UserDeckList.DeckInfos.Count - 1);

                Configuration.Logger.LogDebug($"After delete: SelectedDeckIndex = {SelectedDeckIndex}, ActiveIndex = {UserDeckList.ActiveIndex}");

                SaveDeckList();
            }
        }

        public void OnNavigatedTo()
        {

        }

        public void OnNavigatedFrom() 
        {

        }
    }
}
