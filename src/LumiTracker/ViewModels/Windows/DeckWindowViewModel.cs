using System.Collections.ObjectModel;
using Wpf.Ui.Controls;

using LumiTracker.Config;
using LumiTracker.Models;
using LumiTracker.Views.Windows;
using LumiTracker.Helpers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;

namespace LumiTracker.ViewModels.Windows
{
    public partial class DeckWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<EventCardView> _myEventCardsPlayed = new ();

        [ObservableProperty]
        private ObservableCollection<EventCardView> _opEventCardsPlayed = new ();

        [ObservableProperty]
        private bool _isShowing = false;

        public DeckWindowViewModel()
        {
        }
    }
}
