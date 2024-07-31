using Wpf.Ui.Controls;
using LumiTracker.Config;
using Microsoft.Extensions.Logging;
using LumiTracker.Models;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Windows.Input;

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

    public class ControlButton
    {
        public string Text { get; }
        public SymbolRegular Icon { get; }
        public ICommand ClickCommand { get; }

        public ControlButton(string text, SymbolRegular icon, ICommand command)
        {
            Text = text; 
            Icon = icon;
            ClickCommand = command;
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
        private ObservableCollection<ControlButton> _buttons = [];

        private static readonly string UserDecksDescPath = Path.Combine(
            Configuration.ConfigDir,
            "decks.json"
        );

        public DeckViewModel()
        {
            /////////////////////////
            // Init buttons
            Buttons = new ObservableCollection<ControlButton>
            {
                new ControlButton ( "设为出战牌组", SymbolRegular.Checkmark24, Button1ClickCommand ),
                new ControlButton ( "编辑牌组名称",        SymbolRegular.Pen24, Button1ClickCommand ),
                new ControlButton ( "重新导入牌组",      SymbolRegular.ArrowSync24, Button1ClickCommand ),
                new ControlButton ( "添加牌组",      SymbolRegular.AddCircle24, Button1ClickCommand ),
                new ControlButton ( "删除牌组",      SymbolRegular.Delete24, Button1ClickCommand ),
            };

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
        private void OnButton1Click()
        {
            Configuration.Logger.LogDebug("OnButton1Click");
        }

        public void OnNavigatedTo()
        {
            
        }

        public void OnNavigatedFrom() 
        {

        }
    }
}
