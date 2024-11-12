using LumiTracker.Config;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;

namespace LumiTracker.Models
{
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

    public partial class DeckStatistics : ObservableObject
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
        private ObservableCollection<MatchupStats> _matchupStats;

        [ObservableProperty]
        private ObservableCollection<DuelRecord> _duelRecords;

        public DeckStatistics()
        {
            MatchupStats = [new(), new(), new(), new()];
            DuelRecords = [new(), new(), new(), new()];
            Wins = 22;
            Totals = 39;
            AvgRounds = 7.2f;
            AvgDuration = 685;
        }
    }
}
