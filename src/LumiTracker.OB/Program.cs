using LumiTracker.Config;
using LumiTracker.Models;
using LumiTracker.ViewModels;
using LumiTracker.ViewModels.Pages;
using LumiTracker.ViewModels.Windows;

namespace LumiTracker.OB
{
    class Program
    {
        static void Main(string[] args)
        {
            // my model
            DeckViewModel deckViewModel = new DeckViewModel(null, null);
            GameWatcher gameWatcher = new GameWatcher();
            DeckWindowViewModel my = new DeckWindowViewModel(deckViewModel, gameWatcher);
            gameWatcher.Start("YuanShen.exe");
            gameWatcher.Wait();
        }
    }
}