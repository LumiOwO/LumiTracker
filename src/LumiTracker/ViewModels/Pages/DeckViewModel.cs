﻿using Wpf.Ui.Controls;
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
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using Newtonsoft.Json;
using System.Text;

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
        private DeckInfo Info { get; }

        [ObservableProperty]
        private BuildStats _total = new ();

        // This is ensured to have at least 1 BuildStats
        [ObservableProperty]
        private ObservableCollection<BuildStats> _allBuildStats = [];

        /////////////////////////
        // UI
        [ObservableProperty]
        private BuildStats _current;

        public BuildStats SelectedBuildVersion => 
            AllBuildStats[Math.Clamp(Info.CurrentVersionIndex ?? 0, 0, AllBuildStats.Count - 1)];

        public DeckStatistics(DeckInfo info)
        {
            Info = info;
            Info.PropertyChanged += OnDeckInfoPropertyChanged;

            var first = new BuildStats(Info.ShareCode, Info.CreatedAt);
            _allBuildStats.Add(first);
            if (info.EditVersions != null)
            {
                foreach (BuildEdit edit in info.EditVersions)
                {
                    var stats = new BuildStats(edit.ShareCode, edit.CreatedAt);
                    _allBuildStats.Add(stats);
                }
            }

            _current = _allBuildStats[0];
        }

        public void UpdateCurrent(bool? IncludeAllBuildVersions, int? CurrentVersionIndex)
        {
            bool IsIncludeAllBuildVersions = IncludeAllBuildVersions ?? false;
            int index = Math.Clamp(CurrentVersionIndex ?? 0, 0, AllBuildStats.Count - 1);
            Configuration.Logger.LogDebug($"[UpdateCurrent] IsTotal = {IsIncludeAllBuildVersions}, Index = {index}");
            if (!IsIncludeAllBuildVersions)
            {
                Current = AllBuildStats[index];
                Task task = AsyncLoadAt(index);
            }
            else
            {
                Current = Total;
                Task task = AsyncLoadTotal();
            }
            OnPropertyChanged(nameof(SelectedBuildVersion));
        }

        public async Task AsyncLoadTotal()
        {
            bool lockTaken = false;
            var loadLock = Total.LoadStateLock;
            try
            {
                loadLock.Enter(ref lockTaken);
                if (Total.LoadState >= ELoadState.Loading) return;

                Total.LoadState = ELoadState.Loading;
            }
            finally
            {
                if (lockTaken) loadLock.Exit();
                lockTaken = false;
            }

            try
            {
                Configuration.Logger.LogDebug($"[AsyncLoadTotal] Begin to load Total...");
                var tasks = new List<Task>();
                for (int i = 0; i < AllBuildStats.Count; i++)
                {
                    tasks.Add(AsyncLoadAt(i));
                }
                await Task.WhenAll(tasks);

                List<DuelRecord> records = [];
                foreach (BuildStats stats in AllBuildStats)
                {
                    records.AddRange(stats.DuelRecords);
                }
                Total.SetRecords(records);

                loadLock.Enter(ref lockTaken);
                Total.LoadState = ELoadState.Loaded;

                Configuration.Logger.LogDebug($"[AsyncLoadTotal] Total loaded.");
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"[AsyncLoadTotal] Failed to load Total. {ex.Message}");
                if (!lockTaken)
                {
                    loadLock.Enter(ref lockTaken);
                }
                Total.LoadState = ELoadState.NotLoaded;
            }
            finally
            {
                if (lockTaken) loadLock.Exit();
            }
        }

        public async Task AsyncLoadAt(int index)
        {
            BuildStats stats = AllBuildStats[index];
            bool lockTaken = false;
            var loadLock = stats.LoadStateLock;
            try
            {
                loadLock.Enter(ref lockTaken);
                if (stats.LoadState >= ELoadState.Loading) return;

                stats.LoadState = ELoadState.Loading;
            }
            finally
            {
                if (lockTaken) loadLock.Exit();
                lockTaken = false;
            }

            try
            {
                Configuration.Logger.LogDebug($"[AsyncLoadAt({index})] Begin to load BuildStats...");
                await stats.LoadDataAsync();

                loadLock.Enter(ref lockTaken);
                stats.LoadState = ELoadState.Loaded;
                Configuration.Logger.LogDebug($"[AsyncLoadAt({index})] BuildStats loaded.");
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"[AsyncLoadAt({index})] Failed to load BuildStat. {ex.Message}");
                if (!lockTaken)
                {
                    loadLock.Enter(ref lockTaken);
                }
                stats.LoadState = ELoadState.NotLoaded;
            }
            finally
            {
                if (lockTaken) loadLock.Exit();
            }
        }

        public async Task AddRecord(DuelRecord record)
        {
            BuildStats stats = SelectedBuildVersion;

            // Add record to current
            bool lockTaken = false;
            var loadLock = stats.LoadStateLock;
            try
            {
                loadLock.Enter(ref lockTaken);
                if (stats.LoadState != ELoadState.Loaded)
                {
                    Configuration.Logger.LogError("[AddRecord] Try to add a record, but the build is not loaded!");
                    return;
                }
            }
            finally
            {
                if (lockTaken) loadLock.Exit();
                lockTaken = false;
            }

            record.Expired = (record.EndTime <= stats.Edit.CreatedAt);
            stats.AddRecord(record);
            try
            {
                await SaveRecordToDisk(record, stats.Guid);
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"[AddRecord] Failed to save record to disk: {ex.Message}");
            }


            // Add record to total if Total loaded
            loadLock = Total.LoadStateLock;
            bool totalLoaded = false;
            try
            {
                loadLock.Enter(ref lockTaken);
                totalLoaded = (Total.LoadState == ELoadState.Loaded);
            }
            finally
            {
                if (lockTaken) loadLock.Exit();
                lockTaken = false;
            }

            if (totalLoaded)
            {
                Total.AddRecord(record);
            }
        }

        private async Task SaveRecordToDisk(DuelRecord record, Guid guid)
        {
            string dataDir = Path.Combine(Configuration.DeckBuildsDir, guid.ToString());
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            string jsonPath = Path.Combine(dataDir, $"{DateTime.Now:yyyyMM}.json");

            // Open the file in append mode
            using (var stream = new FileStream(jsonPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                bool empty = (stream.Length == 0);
                if (!empty)
                {
                    // Move back to overwrite the closing bracket ']'
                    stream.Seek(-1, SeekOrigin.End);
                }

                using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
                using (var jsonWriter = new CustomJsonTextWriter(writer, indented: true))
                {
                    if (empty)
                    {
                        // File is empty, start a new JSON array
                        await writer.WriteLineAsync("[");
                    }
                    else
                    {
                        await writer.WriteLineAsync(",");
                    }
                    var serializer = new JsonSerializer();
                    serializer.Serialize(jsonWriter, record);
                    await writer.WriteLineAsync();
                    await writer.WriteAsync("]");
                }
            }
            Configuration.Logger.LogDebug("Record saved to disk.");
        }

        private void OnDeckInfoPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DeckInfo.IncludeAllBuildVersions) || e.PropertyName == nameof(DeckInfo.CurrentVersionIndex))
            {
                UpdateCurrent(Info.IncludeAllBuildVersions, Info.CurrentVersionIndex);
            }
        }
    }

    public partial class DeckItem : ObservableObject
    {
        [ObservableProperty]
        private DeckInfo _info;

        [ObservableProperty]
        private DeckStatistics _stats;

        [ObservableProperty]
        private FontWeight _weightInNameList = FontWeights.Light;

        [ObservableProperty]
        private Brush? _colorInNameList = (SolidColorBrush?)Application.Current?.Resources?["TextFillColorPrimaryBrush"];

        public DeckItem(DeckInfo deckInfo)
        {
            _info  = deckInfo;
            _stats = new DeckStatistics(deckInfo);
        }

        public void LoadCurrent()
        {
            Stats.UpdateCurrent(Info.IncludeAllBuildVersions, Info.CurrentVersionIndex);
        }
    }

    public delegate void OnSelectedCurrentVersionIndexChangedCallback(DeckItem? SelectedDeckItem);

    public partial class DeckViewModel : ObservableObject, INavigationAware
    {
        [ObservableProperty]
        private ObservableCollection<DeckItem> _deckItems = [];

        [ObservableProperty]
        private int _selectedIndex = -1;

        [ObservableProperty]
        private int _activeDeckIndex = -1;

        [ObservableProperty]
        private DeckItem? _selectedDeckItem = null;

        public event OnSelectedCurrentVersionIndexChangedCallback? SelectedCurrentVersionIndexChanged;
        public bool IsChangingDeckItem { get; private set; } = false;

        [ObservableProperty]
        private ControlButton[] _buttons = new ControlButton[(int)EControlButtonType.NumControlButtons];

        private ISnackbarService? SnackbarService;

        private StyledContentDialogService? ContentDialogService;

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

            LoadDeckInformations();
        }

        partial void OnSelectedIndexChanged(int oldValue, int newValue)
        {
            Configuration.Logger.LogDebug($"[OnSelectedIndexChanged] {oldValue} -> {newValue}");
            Select(newValue);
        }

        public static readonly SolidColorBrush HighlightColor = new SolidColorBrush(Color.FromArgb(0xff, 0xf9, 0xca, 0x24));
        partial void OnActiveDeckIndexChanged(int oldValue, int newValue)
        {
            Configuration.Logger.LogDebug($"[OnActiveDeckIndexChanged] {oldValue} -> {newValue}");
            if (oldValue >= 0 && oldValue < DeckItems.Count)
            {
                DeckItems[oldValue].WeightInNameList = FontWeights.Light;
                DeckItems[oldValue].ColorInNameList  = (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"];
            }
            if (newValue >= 0 && newValue < DeckItems.Count)
            {
                DeckItems[newValue].WeightInNameList = FontWeights.Bold;
                DeckItems[newValue].ColorInNameList  = HighlightColor;
                DeckItems[newValue].LoadCurrent();
            }
        }

        public void AddRecordToActiveDeck(DuelRecord record)
        {
            if (ActiveDeckIndex < 0 || ActiveDeckIndex >= DeckItems.Count)
            {
                Configuration.Logger.LogError($"[AddRecordToActiveDeck] Invalid ActiveDeckIndex {ActiveDeckIndex}");
                return;
            }

            Application.Current.Dispatcher.Invoke(async () =>
            {
                await DeckItems[ActiveDeckIndex].Stats.AddRecord(record);
            });
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

            IsChangingDeckItem = true;
            if (valid)
            {
                Configuration.Logger.LogDebug($"Select deck[{index}], {DeckItems.Count} decks in total");
                SelectedDeckItem = DeckItems[index];
                SelectedDeckItem.LoadCurrent();
            }
            else
            {
                SelectedDeckItem = null;
            }
            IsChangingDeckItem = false;
            SelectedCurrentVersionIndexChanged?.Invoke(SelectedDeckItem);
        }

        public void OnCurrentVersionIndexChanged(int CurrentVersionIndex)
        {
            if (SelectedDeckItem == null) return;

            SelectedDeckItem.Info.CurrentVersionIndex = CurrentVersionIndex;
        }

        private static readonly string UserDecksDescPath = Path.Combine(Configuration.ConfigDir, "decks.json");
        public void LoadDeckInformations()
        {
            if (!File.Exists(UserDecksDescPath))
            {
                return;
            }

            try
            {
                var userDecksDesc = Configuration.LoadJObject(UserDecksDescPath);
                if (userDecksDesc.Count == 0)
                {
                    throw new Exception("Loaded an empty desc file.");
                }

                List<DeckItem> deckItems = new ();
                foreach (var jItem in (userDecksDesc["decks"] as JArray)!)
                {
                    DeckInfo info = jItem.ToObject<DeckInfo>()!;
                    info.PropertyChanged += OnDeckInfoPropertyChanged;
                    deckItems.Add(new DeckItem(info));
                }
                DeckItems = new ObservableCollection<DeckItem>(deckItems);

                // no longer need active index save & load
                // int active = (int)userDecksDesc["active"]!;
                // ActiveDeckIndex = Math.Clamp(active, -1, DeckItems.Count - 1);
                // SelectedIndex = ActiveDeckIndex;
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"Failed to load user decks: {ex.Message}");
            }
        }

        public void SaveDeckInformations()
        {
            List<DeckInfo> deckInfos = [];
            foreach (var item in DeckItems)
            {
                deckInfos.Add(item.Info);
            }

            Configuration.SaveJObject(JObject.FromObject(new
            {
                decks = deckInfos,
            }), UserDecksDescPath);
        }

        private bool DisableAutoSaveWhenDeckInfoChanged { get; set; } = false;
        private void OnDeckInfoPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DeckInfo.LastModified))
            {
                return;
            }

            if (SelectedDeckItem != null && SelectedDeckItem.Info == sender)
            {
                SelectedDeckItem.Info.LastModified = DateTime.Now;
            }
            if (e.PropertyName == nameof(DeckInfo.CurrentVersionIndex))
            {
                SelectedCurrentVersionIndexChanged?.Invoke(SelectedDeckItem);
            }

            if (!DisableAutoSaveWhenDeckInfoChanged)
            {
                SaveDeckInformations();
            }
        }

        private void AddNewDeck(string sharecode, int[] cards)
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
                Name         = deckName,
                LastModified = DateTime.Now,
                CreatedAt    = DateTime.Now,
            };
            info.PropertyChanged += OnDeckInfoPropertyChanged;
            DeckItems.Add(new DeckItem(info));
            SelectedIndex = DeckItems.Count - 1;
            SaveDeckInformations();
        }

        private void ReimportDeck(string sharecode, int[] cards)
        {
            var item = SelectedDeckItem!;
            // If characters changed, create a new deck
            string oldKey = DeckUtils.CharacterIdsToKey(item.Stats.SelectedBuildVersion.CharacterIds, ignoreOrder: true);
            string newKey = DeckUtils.CharacterIdsToKey(cards[0], cards[1], cards[2], ignoreOrder: true);
            if (oldKey != newKey)
            {
                AddNewDeck(sharecode, cards);
                return;
            }

            // If build already added, change current build version to it
            Guid guid = DeckUtils.DeckBuildGuid(cards);
            var allStats = item.Stats.AllBuildStats;
            for (int i = 0; i < allStats.Count; i++)
            {
                if (allStats[i].Guid == guid)
                {
                    item.Info.CurrentVersionIndex = i;
                    return;
                }
            }

            // Add new build version
            DisableAutoSaveWhenDeckInfoChanged = true;
            BuildEdit edit = new BuildEdit() { ShareCode = sharecode, CreatedAt = DateTime.Now };
            if (item.Info.EditVersions != null)
            {
                item.Info.EditVersions.Add(edit);
            }
            else
            {
                item.Info.EditVersions = [edit];
            }
            item.Stats.AllBuildStats.Add(new BuildStats(edit));
            item.Info.CurrentVersionIndex = item.Stats.AllBuildStats.Count - 1;

            DisableAutoSaveWhenDeckInfoChanged = false;
            SaveDeckInformations();
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
            //SaveDeckInformations();
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
                int[]? cards = DeckUtils.DecodeShareCode(sharecode);
                if (cards != null)
                {
                    ReimportDeck(sharecode, cards);
                }
                else
                {
                    Configuration.Logger.LogWarning($"[OnReimportDeckClicked] Invalid share code: {sharecode}");
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
                    AddNewDeck(sharecode, cards);
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

                SaveDeckInformations();
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
