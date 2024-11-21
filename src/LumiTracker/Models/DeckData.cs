﻿using LumiTracker.Config;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Swordfish.NET.Collections;

namespace LumiTracker.Models
{
    public partial class BuildEdit : ObservableObject
    {
        [JsonProperty("sharecode")]
        [ObservableProperty]
        [property: JsonIgnore]
        private string _shareCode = "";

        [JsonProperty("created_at")]
        [ObservableProperty]
        [property: JsonIgnore]
        [JsonConverter(typeof(CustomDateTimeConverter), "yyyy/MM/dd HH:mm:ss")]
        private DateTime _createdAt = CustomDateTimeConverter.MinTime;
    }

    public partial class DeckInfo : ObservableObject
    {
        [JsonProperty("sharecode")]
        [ObservableProperty]
        [property: JsonIgnore]
        private string _shareCode = "";

        [JsonProperty("name")]
        [ObservableProperty]
        [property: JsonIgnore]
        private string _name = Lang.DefaultDeckName;

        [JsonProperty("created_at")]
        [ObservableProperty]
        [property: JsonIgnore]
        [JsonConverter(typeof(CustomDateTimeConverter), "yyyy/MM/dd HH:mm:ss")]
        private DateTime _createdAt = CustomDateTimeConverter.MinTime;

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
        private bool _expired = false;

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

        public BuildStats(string sharecode, DateTime CreatedAt)
        {
            Edit = new BuildEdit { ShareCode = sharecode, CreatedAt = CreatedAt };

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

        public BuildStats(BuildEdit edit) : this(edit.ShareCode, edit.CreatedAt)
        {

        }

        public BuildStats()
        {
            Edit = new BuildEdit { ShareCode = "", CreatedAt = DateTime.MinValue };
        }

        public async Task LoadDataAsync()
        {
            await Task.Delay(1000); /// TODO: remove this

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

            // TODO: use guid to load
            List<DuelRecord> records = [
                new(98, 81, 93){ IsWin = true,  Rounds = 7, Duration = 580, TimeStamp = new DateTime(2024, 11, 15, 19, 0, 0), Expired = true},
                new(98, 81, 93){ IsWin = false, Rounds = 6, Duration = 620, TimeStamp = new DateTime(2024, 11, 15, 20, 0, 0), },
                new(27, 73, 88){ IsWin = false,  Rounds = 5, Duration = 300, TimeStamp = new DateTime(2024, 11, 15, 21, 0, 0), },
                new(27, 73, 88){ IsWin = false,  Rounds = 5, Duration = 300, TimeStamp = new DateTime(2024, 11, 15, 21, 0, 0), },
                new(27, 73, 88){ IsWin = false,  Rounds = 5, Duration = 300, TimeStamp = new DateTime(2024, 11, 15, 21, 0, 0), },
                new(31, -1, -1){ IsWin = false,  Rounds = 5, Duration = 300, TimeStamp = new DateTime(2024, 11, 15, 21, 0, 0), },
                new(-1, 21, -1){ IsWin = false,  Rounds = 5, Duration = 300, TimeStamp = new DateTime(2024, 11, 15, 21, 0, 0), },
                new(-1, 21, -1){ IsWin = false,  Rounds = 5, Duration = 300, TimeStamp = new DateTime(2024, 11, 15, 21, 0, 0), },
                ];

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
            DuelRecords = new ObservableCollection<DuelRecord>(records.OrderByDescending(x => x.TimeStamp));
        }

        private void AddRecordToStats(DuelRecord record, 
            ConcurrentObservableSortedSet<MatchupStats> allStats, ConcurrentObservableSortedSet<MatchupStats> afterStats)
        {
            // TODO: need QA
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
