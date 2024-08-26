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
            using (Configuration.Logger.BeginScope("my"))
            {
                gameWatcher.Start("YuanShen.exe");
            }

            // my model
            DeckViewModel opdeckViewModel = new DeckViewModel(null, null);
            GameWatcher opgameWatcher = new GameWatcher();
            DeckWindowViewModel op = new DeckWindowViewModel(opdeckViewModel, opgameWatcher);
            using (Configuration.Logger.BeginScope("op"))
            {
                opgameWatcher.Start("YuanShen.exe");
            }

            gameWatcher.Wait();
            opgameWatcher.Wait();
        }
    }
}