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
        private string _name = LocalizationSource.Instance["DefaultDeckName"];

        [JsonProperty("characters")]
        public int[] Characters = [];

        [JsonIgnore]
        [ObservableProperty]
        private FontWeight _textWeight = FontWeights.Normal;

        [JsonIgnore]
        [ObservableProperty]
        private Brush _textColor = (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"];

        [JsonIgnore]
        [ObservableProperty]
        private Visibility _iconVisibility = Visibility.Collapsed;
    }

    public partial class DeckList : ObservableObject
    {
        [JsonProperty("selected")]
        public int Selected { get; set; } = -1;

        [JsonProperty("decks")]
        [ObservableProperty]
        private ObservableCollection<DeckInfo> _deckInfos = [];
    }

    enum EControlButtonType : int
    {
        SetAsActiveDeck,
        EditDeckName,
        ReimportDeck,
        ShareDeck,
        AddNewDeck,
        DeleteDeck,

        NumControlButtons
    }

    public partial class ControlButton : DependencyObject
    {
        public LocalizationTextItem TextItem { get; }
        public SymbolRegular Icon { get; }
        public ICommand ClickCommand { get; }
        public ControlAppearance Appearance { get; }

        public ControlButton(string textLocalizationKey, SymbolRegular icon, ICommand command, ControlAppearance appearance)
        {
            TextItem = new LocalizationTextItem();
            var binding = LocalizationExtension.Create(textLocalizationKey);
            BindingOperations.SetBinding(TextItem, LocalizationTextItem.TextProperty, binding);

            Icon = icon;
            ClickCommand = command;
            Appearance = appearance;
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
                "SetAsActiveDeck", SymbolRegular.Checkmark24, SetAsActiveDeckClickedCommand, ControlAppearance.Info);
            Buttons[(int)EControlButtonType.EditDeckName] = new ControlButton(
                "EditDeckName", SymbolRegular.Pen24, EditDeckNameClickedCommand, ControlAppearance.Secondary);
            Buttons[(int)EControlButtonType.ReimportDeck] = new ControlButton(
                "ReimportDeck", SymbolRegular.ArrowSync24, ReimportDeckClickedCommand, ControlAppearance.Secondary);
            Buttons[(int)EControlButtonType.ShareDeck] = new ControlButton(
                "ShareDeck", SymbolRegular.Share24, ShareDeckClickedCommand, ControlAppearance.Secondary);
            Buttons[(int)EControlButtonType.AddNewDeck] = new ControlButton(
                "AddNewDeck", SymbolRegular.AddCircle24, AddNewDeckClickedCommand, ControlAppearance.Secondary);
            Buttons[(int)EControlButtonType.DeleteDeck] = new ControlButton(
                "DeleteDeck", SymbolRegular.Delete24, DeleteDeckClickedCommand, ControlAppearance.Danger);

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
            _userDeckList = userDecks;

            if (UserDeckList.DeckInfos.Count == 0)
            {
                return;
            }
            int selected = UserDeckList.Selected;
            if (selected < 0)
            {
                selected = 0;
            }
            Select(selected);
        }

        private void Select(int index)
        {
            DeckInfo info = UserDeckList.DeckInfos[index];
            string sharecode = info.ShareCode;
            int[]? cards = DeckUtils.DecodeShareCode(sharecode);
            if (cards == null)
            {
                Configuration.Logger.LogWarning($"Invalid share code: {sharecode}");
                return;
            }
            UserDeckList.Selected = index;
            info.ShareCode = sharecode;

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

        [RelayCommand]
        private void OnSetAsActiveDeckClicked()
        {
            Configuration.Logger.LogDebug("OnSetAsActiveDeckClicked");
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
