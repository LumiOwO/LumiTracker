using System.Collections.ObjectModel;
using Wpf.Ui.Controls;

using LumiTracker.Config;
using LumiTracker.Models;
using LumiTracker.Views.Windows;
using LumiTracker.Helpers;

namespace LumiTracker.ViewModels.Windows
{
    public partial class DeckWindowViewModel : ObservableObject
    {
        private GameWatcher _gameWatcher;


        public DeckWindowViewModel(GameWatcher gameWatcher)
        {
            _gameWatcher = gameWatcher;
        }
    }
}
