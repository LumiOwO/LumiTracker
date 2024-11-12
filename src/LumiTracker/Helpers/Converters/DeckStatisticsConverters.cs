using LumiTracker.Config;
using LumiTracker.Models;
using System.Globalization;
using System.Windows.Data;

namespace LumiTracker.Helpers
{
    public class FloatToMinuteSecondsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string timeStr = "--:--";
            if (value is float floatValue)
            {
                TimeSpan time = TimeSpan.FromSeconds(floatValue);
                timeStr = $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
            }
            return timeStr;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class WinRateFromStatsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            float winRate = 0;
            if (value is MatchupStats matchupStats && matchupStats.Totals > 0)
            {
                winRate = 1.0f * matchupStats.Wins / matchupStats.Totals;
            }
            else if (value is DeckStatistics deckStats && deckStats.Totals > 0)
            {
                winRate = 1.0f * deckStats.Wins / deckStats.Totals;
            }
            return winRate;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
