using LumiTracker.Config;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using System.Windows.Media;

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
        public ObservableCollection<BuildEdit>? _editVersionCodes = null;

        [JsonProperty("current")]
        [ObservableProperty]
        [property: JsonIgnore]
        private int? _currentVersionIndex = null;

        [JsonProperty("current_version_only")]
        [ObservableProperty]
        [property: JsonIgnore]
        private bool? _showCurrentVersionOnly = null;

        [JsonProperty("hide_expired")]
        [ObservableProperty]
        [property: JsonIgnore]
        private bool? _hideRecordsBeforeImport = null;
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

        [ObservableProperty]
        private SolidColorBrush _WinRateColor = new SolidColorBrush(Color.FromArgb(0xff, 0x4c, 0xd1, 0x37));

        public double AvgDurationInMinutes => AvgDuration / 60.0; // Minutes

        public double WinRate => Math.Max(Wins, 0.0) / Math.Max(Totals, 1.0);

        partial void OnAvgDurationChanged(double oldValue, double newValue)
        {
            OnPropertyChanged("AvgDurationInMinutes");
        }

        partial void OnWinsChanged(int oldValue, int newValue)
        {
            OnPropertyChanged("WinRate");
        }

        partial void OnTotalsChanged(int oldValue, int newValue)
        {
            OnPropertyChanged("WinRate");
        }
    }

    public partial class DuelRecord : ObservableObject
    {
        [ObservableProperty]
        private bool _isWin;
        [ObservableProperty]
        private double _duration; // seconds
        [ObservableProperty]
        private int _rounds;
        [ObservableProperty]
        private DateTime _timeStamp;
        [ObservableProperty]
        private List<int> _opCharacters;

        public double DurationInMinutes => Duration / 60.0; // Minutes

        partial void OnDurationChanged(double oldValue, double newValue)
        {
            OnPropertyChanged("DurationInMinutes");
        }

        public DuelRecord()
        {
            _isWin = false;
            _duration = 300;
            _rounds = 7;
            _timeStamp = DateTime.Now;
            _opCharacters = [19, 50, 51];
        }
    }

    public partial class BuildStats : ObservableObject
    {
        public Guid Guid { get; set; } = Guid.Empty;

        [ObservableProperty]
        private bool _loaded = false;

        [ObservableProperty]
        private DateTime _createdAt = CustomDateTimeConverter.MinTime;

        [ObservableProperty]
        private MatchupStats _summary;

        [ObservableProperty]
        private ObservableCollection<MatchupStats> _allMatchupStats;

        [ObservableProperty]
        private ObservableCollection<DuelRecord> _duelRecords;

        public BuildStats()
        {
            Summary = new() { Totals = 3, Wins = 2, AvgRounds = 6.0, AvgDuration = 500 };
            AllMatchupStats = [
                new(){ Totals = 2, Wins = 1, AvgRounds = 6.5, AvgDuration = 600, OpCharacters = [98, 81, 93] },
                new(){ Totals = 1, Wins = 0, AvgRounds = 5, AvgDuration = 300, OpCharacters = [27, 73, 88]}];
            DuelRecords = [
                new(){ IsWin = true,  Rounds = 7, Duration = 580, TimeStamp = new DateTime(2024, 11, 15, 19, 0, 0), OpCharacters = [98, 81, 93] },
                new(){ IsWin = false, Rounds = 6, Duration = 620, TimeStamp = new DateTime(2024, 11, 15, 20, 0, 0), OpCharacters = [98, 81, 93] },
                new(){ IsWin = false,  Rounds = 5, Duration = 300, TimeStamp = new DateTime(2024, 11, 15, 21, 0, 0), OpCharacters = [27, 73, 88] },
                ];
        }
    }
}
