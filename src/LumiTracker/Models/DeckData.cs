using LumiTracker.Config;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using System.Windows.Media;

namespace LumiTracker.Models
{
    public partial class DeckInfo : ObservableObject
    {
        /////////////////////////
        // Serializable
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

        [JsonProperty("last_modified")]
        [JsonConverter(typeof(CustomDateTimeConverter), "yyyy/MM/dd HH:mm:ss")]
        public DateTime LastModified = CustomDateTimeConverter.MinTime;

        [JsonProperty("versions")]
        [ObservableProperty]
        [property: JsonIgnore]
        public ObservableCollection<string>? _buildVersions = null;

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
        private int _wins;
        [ObservableProperty]
        private int _totals;
        [ObservableProperty]
        private float _avgRounds;
        [ObservableProperty]
        private float _avgDuration; // seconds
        [ObservableProperty]
        private List<int> _opCharacters;

        public MatchupStats()
        {
            _wins = 20;
            _totals = 50;
            _avgRounds = 6.0f;
            _avgDuration = 600;
            _opCharacters = [9, 10, 11];
        }
    }

    public partial class DuelRecord : ObservableObject
    {
        [ObservableProperty]
        private bool _isWin;
        [ObservableProperty]
        private float _duration;
        [ObservableProperty]
        private int _rounds;
        [ObservableProperty]
        private DateTime _timeStamp;
        [ObservableProperty]
        private List<int> _opCharacters;

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
        [ObservableProperty]
        private MatchupStats _summary;

        [ObservableProperty]
        private ObservableCollection<MatchupStats> _allMatchupStats;

        [ObservableProperty]
        private ObservableCollection<DuelRecord> _duelRecords;

        public BuildStats()
        {
            Summary = new();
            AllMatchupStats = [new(), new(), new(), new(),];
            DuelRecords = [new(), new(), new(), new(),];
        }
    }

}
