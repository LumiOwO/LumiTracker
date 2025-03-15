using LumiTracker.Config;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Swordfish.NET.Collections;
using System.IO;
using System.ComponentModel;

namespace LumiTracker.Models
{
    public partial class BuildEdit : ObservableObject
    {
        [JsonProperty("sharecode")]
        [ObservableProperty]
        [property: JsonIgnore]
        private string _shareCode = "";

        [JsonProperty("name")]
        [ObservableProperty]
        [property: JsonIgnore]
        private string? _name = null;

        [JsonProperty("created_at")]
        [ObservableProperty]
        [property: JsonIgnore]
        [JsonConverter(typeof(CustomDateTimeConverter), "yyyy/MM/dd HH:mm:ss")]
        private DateTime _createdAt = CustomDateTimeConverter.MinTime;

        [property: JsonIgnore]
        public DeckInfo? Info { get; set; } = null;

        public BuildEdit(DeckInfo? info)
        {
            Info = info;
            // Subscribe to the PropertyChanged event
            PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Info?.NotifyEditChanged();
        }
    }

    public partial class DeckInfo : ObservableObject
    {
        // Should be compatible with old format
        [ObservableProperty]
        [property: JsonIgnore]
        private BuildEdit _edit;

        [JsonProperty("last_modified")]
        [ObservableProperty]
        [property: JsonIgnore]
        [JsonConverter(typeof(CustomDateTimeConverter), "yyyy/MM/dd HH:mm:ss")]
        private DateTime _lastModified = CustomDateTimeConverter.MinTime;

        [JsonProperty("edits")]
        [ObservableProperty]
        [property: JsonIgnore]
        private ObservableCollection<BuildEdit>? _editVersions = null;

        [JsonProperty("current")]
        [ObservableProperty]
        [property: JsonIgnore]
        private int? _currentVersionIndex = null;

        [JsonProperty("all_versions")]
        [ObservableProperty]
        [property: JsonIgnore]
        private bool? _includeAllBuildVersions = null;

        [JsonProperty("hide_expired")]
        [ObservableProperty]
        [property: JsonIgnore]
        private bool? _hideRecordsBeforeImport = null;

        public DeckInfo()
        {
            _edit = new BuildEdit(this);
        }

        public void NotifyEditChanged()
        {
            OnPropertyChanged(nameof(Edit));
        }
    }

    public partial class MatchupStats : ObservableObject
    {
        [ObservableProperty]
        private int _wins = 0;
        [ObservableProperty]
        private int _totals = 0;
        [ObservableProperty]
        private double _avgRounds = 0;
        [ObservableProperty]
        private double _avgDuration = 0; // seconds
        [ObservableProperty]
        private List<int> _opCharacters = [-1, -1, -1];

        public string Key => DeckUtils.CharacterIdsToKey(OpCharacters, ignoreOrder: true);

        public MatchupStats(int cid0, int cid1, int cid2)
        {
            _opCharacters = [cid0, cid1, cid2];
        }

        /////////////////////
        // UI
        public double AvgDurationInMinutes => AvgDuration / 60.0; // Minutes

        public double WinRate => Math.Max(Wins, 0.0) / Math.Max(Totals, 1.0);

        partial void OnAvgDurationChanged(double oldValue, double newValue)
        {
            OnPropertyChanged(nameof(AvgDurationInMinutes));
        }

        partial void OnWinsChanged(int oldValue, int newValue)
        {
            OnPropertyChanged(nameof(WinRate));
        }

        partial void OnTotalsChanged(int oldValue, int newValue)
        {
            OnPropertyChanged(nameof(WinRate));
        }

        partial void OnOpCharactersChanged(List<int>? oldValue, List<int> newValue)
        {
            OnPropertyChanged(nameof(Key));
        }

        public void AddStats(MatchupStats other)
        {
            // For reference: Brute-force way
            //AvgRounds = ((AvgRounds * Totals) + (other.AvgRounds * other.Totals)) / (Totals + other.Totals);
            double wAll     = 1.0 * Totals       / (Totals + other.Totals);
            double wExpired = 1.0 * other.Totals / (Totals + other.Totals);
            AvgRounds   = AvgRounds   * wAll + other.AvgRounds   * wExpired;
            AvgDuration = AvgDuration * wAll + other.AvgDuration * wExpired;

            Wins   += other.Wins;
            Totals += other.Totals;
        }
    }

    public partial class DuelRecord : ObservableObject
    {
        [JsonProperty("win")]
        [ObservableProperty]
        [property: JsonIgnore]
        private bool _isWin = false;

        [JsonProperty("duration")]
        [ObservableProperty]
        [property: JsonIgnore]
        private double _duration = 0; // seconds

        [JsonProperty("rounds")]
        [ObservableProperty]
        [property: JsonIgnore]
        private int _rounds = 0;

        [JsonProperty("endtime")]
        [ObservableProperty]
        [property: JsonIgnore]
        [JsonConverter(typeof(CustomDateTimeConverter), "yyyy/MM/dd HH:mm:ss")]
        private DateTime _endTime = CustomDateTimeConverter.MinTime;

        [JsonProperty("op")]
        [ObservableProperty]
        [property: JsonIgnore]
        private List<int> _opCharacters = [-1, -1, -1];

        // TODO: add the following two fields into statistics
        [JsonProperty("starting_hand")]
        [ObservableProperty]
        [property: JsonIgnore]
        private List<int> _startingHand = [];

        [JsonProperty("starts_first")]
        [ObservableProperty]
        [property: JsonIgnore]
        private bool? _startsFirst = null;

        public DuelRecord(int cid0, int cid1, int cid2)
        {
            _opCharacters = [cid0, cid1, cid2];
        }

        /////////////////////
        // UI
        [property: JsonIgnore]
        public double DurationInMinutes => Duration / 60.0; // Minutes

        [ObservableProperty]
        [property: JsonIgnore]
        private bool _expired = false;

        partial void OnDurationChanged(double oldValue, double newValue)
        {
            OnPropertyChanged(nameof(DurationInMinutes));
        }

        public MatchupStats GetStats()
        {
            var cids = OpCharacters;
            return new MatchupStats(cids[0], cids[1], cids[2])
            {
                Wins         = IsWin ? 1 : 0,
                Totals       = 1,
                AvgRounds    = Rounds,
                AvgDuration  = Duration,
            };
        }
    }

    public enum ELoadState : int
    {
        NotLoaded,
        Loading,
        Loaded
    }

    public partial class BuildStats : ObservableObject
    {
        [ObservableProperty]
        public ELoadState _loadState = ELoadState.NotLoaded;
        public SpinLock LoadStateLock { get; } = new SpinLock();
        public bool IsLoading => (LoadState == ELoadState.Loading);
        public bool IsLoaded => (LoadState == ELoadState.Loaded);

        partial void OnLoadStateChanged(ELoadState oldValue, ELoadState newValue)
        {
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(IsLoaded));
        }

        /////////////////////////
        // These will be loaded when LoadState == ELoadState.Loaded
        public Guid Guid { get; set; } = Guid.Empty;

        public BuildEdit Edit { get; set; } // Must be init in constructor

        [ObservableProperty]
        private ObservableCollection<ActionCardView> _actionCards = [];

        [ObservableProperty]
        private List<int> _characterIds = [-1, -1, -1];

        public int[] Cards { get; set; } = [];

        [ObservableProperty]
        private ObservableCollection<DuelRecord> _duelRecords = [];

        private static readonly Comparer<MatchupStats> MatchupStatsComparer
            = Comparer<MatchupStats>.Create((x, y) =>
            {
                string x_key = x.Key;
                string y_key = y.Key;
                if (x_key == y_key) return 0;
                // Unknown matchup will be at the bottom
                if (x_key == DeckUtils.UnknownCharactersKey) return 1;
                if (y_key == DeckUtils.UnknownCharactersKey) return -1;

                if (x.Totals != y.Totals)
                {
                    // Descending
                    return -x.Totals.CompareTo(y.Totals);
                }
                else
                {
                    return x_key.CompareTo(y_key);
                }
            });

        [ObservableProperty]
        private ConcurrentObservableSortedSet<MatchupStats> _allMatchupStats = new (MatchupStatsComparer);

        [ObservableProperty]
        private MatchupStats _summary = new(-1, -1, -1);

        [ObservableProperty]
        private ConcurrentObservableSortedSet<MatchupStats> _matchupStatsAfterImport = new (MatchupStatsComparer);

        [ObservableProperty]
        private MatchupStats _summaryAfterImport = new(-1, -1, -1);

        public BuildStats(BuildEdit edit)
        {
            Edit = edit;

            int[]? cards = DeckUtils.DecodeShareCode(Edit.ShareCode);
            Guid = DeckUtils.DeckBuildGuid(cards);
            if (cards == null)
            {
                Configuration.Logger.LogWarning($"[BuildStats] Invalid share code: {Edit.ShareCode}");
                cards = Enumerable.Repeat(-1, 33).ToArray();
            }
            else
            {
                // Sort the cards into default order
                Array.Sort(cards, 3, 30,
                    Comparer<int>.Create((x, y) => DeckUtils.ActionCardCompare(x, y)));
            }
            Cards = cards;
            CharacterIds = new List<int>(cards[..3]);
        }

        public BuildStats()
        {
            Edit = new BuildEdit(null) { ShareCode = "", CreatedAt = DateTime.MinValue };
        }

        public async Task LoadDataAsync()
        {
            // To column major
            ActionCardView[] views = new ActionCardView[30];
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 15; y++)
                {
                    // transpose
                    int srcIdx = 3 + x * 15 + y;
                    int dstIdx = y * 2 + x;
                    views[dstIdx] = new ActionCardView(null, Cards[srcIdx], inGame: false);
                }
            }
            ActionCards = new ObservableCollection<ActionCardView>(views);

            List<DuelRecord> records = [];
            string dataDir = Path.Combine(Configuration.DeckBuildsDir, Guid.ToString());
            if (Directory.Exists(dataDir))
            {
                foreach (var filePath in Directory.GetFiles(dataDir, "*.json"))
                {
                    // Extract just the filename
                    var filename = Path.GetFileName(filePath);
                    // Filter
                    if (!(
                        filename.Length == 11 &&        // Total length of "YYYYMM.json"
                        filename.EndsWith(".json") &&   // Ends with ".json"
                        filename[..6].All(char.IsDigit) // First 6 characters are digits
                        ))
                    {
                        continue;
                    }

                    try
                    {
                        // Read and parse each JSON file
                        string fileContent = await File.ReadAllTextAsync(filePath);
                        if (!string.IsNullOrWhiteSpace(fileContent))
                        {
                            var curRecords = JsonConvert.DeserializeObject<List<DuelRecord>>(fileContent);
                            if (curRecords == null)
                            {
                                throw new Exception("Failed to parse json.");
                            }
                            records.AddRange(curRecords);
                        }
                    }
                    catch (Exception ex)
                    {
                        Configuration.Logger.LogError($"Error when parsing {filePath}: {ex.Message}");
                    }
                }
            }

            foreach (var record in records)
            {
                record.Expired = (record.EndTime <= Edit.CreatedAt);
            }
            SetRecords(records);
        }

        public void AddRecord(DuelRecord record)
        {
            AddRecordToStats(record, AllMatchupStats, MatchupStatsAfterImport);
            DuelRecords.Insert(0, record);
        }

        public void SetRecords(List<DuelRecord> records)
        {
            ConcurrentObservableSortedSet<MatchupStats> allStats   = new (MatchupStatsComparer);
            ConcurrentObservableSortedSet<MatchupStats> afterStats = new (MatchupStatsComparer);
            foreach (var record in records)
            {
                AddRecordToStats(record, allStats, afterStats);
            }
            AllMatchupStats         = allStats;
            MatchupStatsAfterImport = afterStats;
            DuelRecords = new ObservableCollection<DuelRecord>(records.OrderByDescending(x => x.EndTime));
        }

        private void AddRecordToStats(DuelRecord record, 
            ConcurrentObservableSortedSet<MatchupStats> allStats, ConcurrentObservableSortedSet<MatchupStats> afterStats)
        {
            MatchupStats stats = record.GetStats();
            string key = stats.Key;
            var cids = stats.OpCharacters;

            MatchupStats? all = allStats.FirstOrDefault(m => m.Key == key);
            if (all == null)
            {
                all = new MatchupStats(cids[0], cids[1], cids[2]);
            }
            else
            {
                // Need to remove from OrderedSet, or the order will not update
                allStats.Remove(all);
            }
            all.AddStats(stats);
            allStats.Add(all);
            Summary.AddStats(stats);

            if (!record.Expired)
            {
                MatchupStats? after = afterStats.FirstOrDefault(m => m.Key == key);
                if (after == null)
                {
                    after = new MatchupStats(cids[0], cids[1], cids[2]);
                }
                else
                {
                    // Need to remove from OrderedSet, or the order will not update
                    afterStats.Remove(after);
                }
                after.AddStats(stats);
                afterStats.Add(after);
                SummaryAfterImport.AddStats(stats);
            }
        }
    }
}
