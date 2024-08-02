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
            if (oldValue >= 0)
            {
                DeckInfos[oldValue].TextWeight     = FontWeights.Light;
                DeckInfos[oldValue].TextColor      = (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"];
            }
            if (newValue >= 0)
            {
                DeckInfos[newValue].TextWeight     = FontWeights.Bold;
                DeckInfos[newValue].TextColor      = new SolidColorBrush(Color.FromArgb(0xff, 0xf9, 0xca, 0x24));
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

        public ControlButton(string textLocalizationKey, SymbolRegular icon, ICommand command, ControlAppearance appearance, bool isEnabled)
        {
            TextItem = new LocalizationTextItem();
            var binding = LocalizationExtension.Create(textLocalizationKey);
            BindingOperations.SetBinding(TextItem, LocalizationTextItem.TextProperty, binding);

            Icon = icon;
            ClickCommand = command;
            Appearance = appearance;
            IsEnabled = isEnabled;
        }
    }

    public partial class DeckViewModel : ObservableObject, INavigationAware
    {
        [ObservableProperty]
        private ObservableCollection<AvatarView> _avatars = new () { new AvatarView(), new AvatarView(), new AvatarView() };

        [ObservableProperty]
        private ObservableCollection<ActionCardView> _currentDeck = new ();

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

        public DeckViewModel()
        {
            /////////////////////////
            // Init buttons
            Buttons[(int)EControlButtonType.SetAsActiveDeck] = new ControlButton(
                "SetAsActiveDeck", SymbolRegular.Checkmark24, SetAsActiveDeckClickedCommand, ControlAppearance.Info, false);
            Buttons[(int)EControlButtonType.EditDeckName] = new ControlButton(
                "EditDeckName", SymbolRegular.Pen24, EditDeckNameClickedCommand, ControlAppearance.Secondary, false);
            Buttons[(int)EControlButtonType.ReimportDeck] = new ControlButton(
                "ReimportDeck", SymbolRegular.ArrowSync24, ReimportDeckClickedCommand, ControlAppearance.Secondary, false);
            Buttons[(int)EControlButtonType.ShareDeck] = new ControlButton(
                "ShareDeck", SymbolRegular.Share24, ShareDeckClickedCommand, ControlAppearance.Secondary, false);
            Buttons[(int)EControlButtonType.AddNewDeck] = new ControlButton(
                "AddNewDeck", SymbolRegular.AddCircle24, AddNewDeckClickedCommand, ControlAppearance.Secondary, true);
            Buttons[(int)EControlButtonType.DeleteDeck] = new ControlButton(
                "DeleteDeck", SymbolRegular.Delete24, DeleteDeckClickedCommand, ControlAppearance.Danger, true);

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
            if (!valid) return;

            ///////////////////////
            // Decode share code
            DeckInfo info = UserDeckList.DeckInfos[index];
            string sharecode = info.ShareCode;
            int[]? cards = DeckUtils.DecodeShareCode(sharecode);
            if (cards == null)
            {
                Configuration.Logger.LogWarning($"Invalid share code: {sharecode}");
                return;
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
                currentDeck.Add(new ActionCardView(card_id));
            }
            CurrentDeck = currentDeck;
        }

        private void SaveDeckList()
        {
            Configuration.SaveJObject(JObject.FromObject(UserDeckList), UserDecksDescPath);
        }


        [RelayCommand]
        private void OnSetAsActiveDeckClicked()
        {
            UserDeckList.ActiveIndex = SelectedDeckIndex;
            SaveDeckList();
        }

        [RelayCommand]
        private void OnEditDeckNameClicked()
        {
            Configuration.Logger.LogDebug("OnEditDeckNameClicked");
        }

        [RelayCommand]
        private void OnReimportDeckClicked()
        {
            Configuration.Logger.LogDebug("OnReimportDeckClicked");
        }

        [RelayCommand]
        private void OnShareDeckClicked()
        {
            Configuration.Logger.LogDebug("OnShareDeckClicked");
        }

        [RelayCommand]
        private void OnAddNewDeckClicked()
        {
            Configuration.Logger.LogDebug("OnAddNewDeckClicked");
        }

        [RelayCommand]
        private void OnDeleteDeckClicked()
        {
            Configuration.Logger.LogDebug("OnDeleteDeckClicked");
        }

        public void OnNavigatedTo()
        {

        }

        public void OnNavigatedFrom() 
        {

        }
    }
}
