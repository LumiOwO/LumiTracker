using LumiTracker.Config;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace LumiTracker.Models
{
    public partial class BuildEdit : ObservableObject
    {
        [JsonProperty("sharecode")]
        public string ShareCode = "";

        [JsonProperty("created_at")]
        [JsonConverter(typeof(CustomDateTimeConverter), "yyyy/MM/dd HH:mm:ss")]
        public DateTime CreatedAt = CustomDateTimeConverter.MinTime;
    }

    public partial class DeckInfo : ObservableObject
    {
        [JsonProperty("sharecode")]
        public string ShareCode = "";

        [JsonProperty("name")]
        [ObservableProperty]
        [property: JsonIgnore]
        private string _name = Lang.DefaultDeckName;

        [JsonProperty("characters")]
        [ObservableProperty]
        [property: JsonIgnore]
        private List<int> _characters = [-1, -1, -1];

        [JsonProperty("created_at")]
        [JsonConverter(typeof(CustomDateTimeConverter), "yyyy/MM/dd HH:mm:ss")]
        public DateTime CreatedAt = CustomDateTimeConverter.MinTime;

        [JsonProperty("last_modified")]
        [JsonConverter(typeof(CustomDateTimeConverter), "yyyy/MM/dd HH:mm:ss")]
        public DateTime LastModified = CustomDateTimeConverter.MinTime;

        [JsonProperty("edits")]
        [ObservableProperty]
        [property: JsonIgnore]
        public ObservableCollection<BuildEdit>? _editVersions = null;

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
    }

    public partial class MatchupStats(int cid0, int cid1, int cid2) : ObservableObject
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
        private List<int> _opCharacters = [cid0, cid1, cid2];

        public string Key => DeckUtils.CharacterIdsToKey(OpCharacters);

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

        public void RemoveStats(MatchupStats other)
        {
            if (Totals <= other.Totals)
            {
                AvgRounds = AvgDuration = Wins = Totals = 0;
                return;
            }

            // For reference: Brute-force way
            //AvgRounds = ((AvgRounds * Totals) - (other.AvgRounds * other.Totals)) / (Totals - other.Totals);
            double wAll     = 1.0 * Totals       / (Totals - other.Totals);
            double wExpired = 1.0 * other.Totals / (Totals - other.Totals);
            AvgRounds   = AvgRounds   * wAll - other.AvgRounds   * wExpired;
            AvgDuration = AvgDuration * wAll - other.AvgDuration * wExpired;

            Wins   -= other.Wins;
            Totals -= other.Totals;
        }
    }

    public partial class DuelRecord(int cid0, int cid1, int cid2) : ObservableObject
    {
        [ObservableProperty]
        private bool _isWin = false;
        [ObservableProperty]
        private double _duration = 0; // seconds
        [ObservableProperty]
        private int _rounds = 0;
        [ObservableProperty]
        private DateTime _timeStamp = CustomDateTimeConverter.MinTime;
        [ObservableProperty]
        private List<int> _opCharacters = [cid0, cid1, cid2];

        /////////////////////
        // UI
        public double DurationInMinutes => Duration / 60.0; // Minutes

        [ObservableProperty]
        private bool _shouldHide = false;

        partial void OnDurationChanged(double oldValue, double newValue)
        {
            OnPropertyChanged(nameof(DurationInMinutes));
        }

        public MatchupStats GetStats()
        {
            return new MatchupStats(cid0, cid1, cid2)
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
        public Guid Guid { get; set; } = Guid.Empty;
        public SpinLock LoadStateLock { get; } = new SpinLock();

        [ObservableProperty]
        public ELoadState _loadState = ELoadState.NotLoaded;

        public bool IsLoading => (LoadState == ELoadState.Loading);

        partial void OnLoadStateChanged(ELoadState oldValue, ELoadState newValue)
        {
            OnPropertyChanged(nameof(IsLoading));
        }

        [ObservableProperty]
        private DateTime _createdAt = new DateTime(2024, 11, 15, 19, 10, 0); // TODO: remove this

        [ObservableProperty]
        private MatchupStats _summary = new (-1, -1, -1);

        [ObservableProperty]
        private ObservableCollection<MatchupStats> _allMatchupStats = [];

        [ObservableProperty]
        private ObservableCollection<DuelRecord> _duelRecords = [];

        // TODO: initialize this
        private bool ShouldHideExpiredRecords { get; set; } = false;
        private MatchupStats SummaryBeforeImport { get; set; } = new (-1, -1, -1);
        private Dictionary<string, MatchupStats> MatchupStatsDataBeforeImport { get; set; } = [];
        private Dictionary<string, MatchupStats> AllMatchupStatsData { get; set; } = [];

        public void NotifyMatchupStatsChanged()
        {
            // Notify MatchupStats ui update 
            AllMatchupStats = new ObservableCollection<MatchupStats>(
                AllMatchupStatsData.Values.OrderByDescending(stats => 
                {
                    if (stats.Key == DeckUtils.UnknownCharactersKey)
                    {
                        return int.MinValue;
                    }
                    return stats.Totals;
                })
            );
        }

        public async Task LoadDataAsync()
        {
            await Task.Delay(1000); /// TODO: remove this
            // TODO: use guid to load
            DuelRecord[] records = [
                new(98, 81, 93){ IsWin = true,  Rounds = 7, Duration = 580, TimeStamp = new DateTime(2024, 11, 15, 19, 0, 0), },
                new(98, 81, 93){ IsWin = false, Rounds = 6, Duration = 620, TimeStamp = new DateTime(2024, 11, 15, 20, 0, 0), },
                new(27, 73, 88){ IsWin = false,  Rounds = 5, Duration = 300, TimeStamp = new DateTime(2024, 11, 15, 21, 0, 0), },
                new(27, 73, 88){ IsWin = false,  Rounds = 5, Duration = 300, TimeStamp = new DateTime(2024, 11, 15, 21, 0, 0), },
                new(27, 73, 88){ IsWin = false,  Rounds = 5, Duration = 300, TimeStamp = new DateTime(2024, 11, 15, 21, 0, 0), },
                new(31, -1, -1){ IsWin = false,  Rounds = 5, Duration = 300, TimeStamp = new DateTime(2024, 11, 15, 21, 0, 0), },
                new(-1, 21, -1){ IsWin = false,  Rounds = 5, Duration = 300, TimeStamp = new DateTime(2024, 11, 15, 21, 0, 0), },
                new(-1, 21, -1){ IsWin = false,  Rounds = 5, Duration = 300, TimeStamp = new DateTime(2024, 11, 15, 21, 0, 0), },
                ];

            foreach (var record in records)
            {
                AddRecord(record, false);
            }
            NotifyMatchupStatsChanged();
        }

        public void AddRecord(DuelRecord record, bool should_notify = true)
        {
            // TODO: need QA
            MatchupStats stats = record.GetStats();
            string key = stats.Key;
            var cids = stats.OpCharacters;

            if (!AllMatchupStatsData.TryGetValue(key, out MatchupStats? all))
            {
                all = new MatchupStats(cids[0], cids[1], cids[2]);
                AllMatchupStatsData[key] = all;
            }
            all.AddStats(stats);
            Summary.AddStats(stats);

            DuelRecords.Insert(0, record);

            if (record.TimeStamp <= CreatedAt)
            {
                if (!MatchupStatsDataBeforeImport.TryGetValue(key, out MatchupStats? expired))
                {
                    expired = new MatchupStats(cids[0], cids[1], cids[2]);
                    MatchupStatsDataBeforeImport[key] = expired;
                }
                expired.AddStats(stats);
                SummaryBeforeImport.AddStats(stats);
            }

            if (should_notify)
            {
                NotifyMatchupStatsChanged();
            }
        }

        public void HideExpiredRecords(bool shouldHide)
        {
            if (ShouldHideExpiredRecords == shouldHide) return;
            ShouldHideExpiredRecords = shouldHide;

            if (shouldHide)
            {
                foreach (var pair in MatchupStatsDataBeforeImport)
                {
                    string key = pair.Key;
                    MatchupStats expired = pair.Value;
                    if (!AllMatchupStatsData.TryGetValue(key, out MatchupStats? all))
                    {
                        Configuration.Logger.LogError($"[HideExpiredRecords] Key {key} not found in AllMatchupStatsData.");
                        continue;
                    }
                    if (all.Totals == expired.Totals)
                    {
                        AllMatchupStatsData.Remove(key);
                        continue;
                    }
                    else if (all.Totals < expired.Totals)
                    {
                        Configuration.Logger.LogError($"[HideExpiredRecords] {key}: Number of expired is greater then number of all.");
                        AllMatchupStatsData.Remove(key);
                        continue;
                    }

                    all.RemoveStats(expired);
                }
                Summary.RemoveStats(SummaryBeforeImport);

                foreach (var record in DuelRecords)
                {
                    record.ShouldHide = record.TimeStamp <= CreatedAt;
                }
            }
            else
            {
                foreach (var pair in MatchupStatsDataBeforeImport)
                {
                    string key = pair.Key;
                    MatchupStats expired = pair.Value;
                    var cids = expired.OpCharacters;

                    MatchupStats? all = null;
                    if (!AllMatchupStatsData.TryGetValue(key, out all))
                    {
                        all = new MatchupStats(cids[0], cids[1], cids[2]);
                        AllMatchupStatsData[key] = all;
                    }
                    
                    all.AddStats(expired);
                }
                Summary.AddStats(SummaryBeforeImport);

                foreach (var record in DuelRecords)
                {
                    record.ShouldHide = false;
                }
            }

            NotifyMatchupStatsChanged();
        }
    }
}
