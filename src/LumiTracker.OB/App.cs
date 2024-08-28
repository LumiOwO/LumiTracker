using LumiTracker.Config;
using LumiTracker.Models;
using LumiTracker.ViewModels.Pages;
using LumiTracker.ViewModels.Windows;

namespace LumiTracker.OB
{
    public class OBApp
    {
        public static void Main(string[] args)
        {
            // my model
            GameWatcher gameWatcher = new GameWatcher();
            using (Configuration.Logger.BeginScope(new ScopeState { Name = "MY", Color = LogHelper.AnsiOrange }))
            {
                DeckViewModel deckViewModel = new DeckViewModel(null, null);
                DeckWindowViewModel my = new DeckWindowViewModel(deckViewModel, gameWatcher);
                gameWatcher.Start("YuanShen.exe");
            }

            // my model
            GameWatcher opgameWatcher = new GameWatcher();
            using (Configuration.Logger.BeginScope(new ScopeState { Name = "OP", Color = LogHelper.AnsiBlue }))
            {
                DeckViewModel opdeckViewModel = new DeckViewModel(null, null);
                DeckWindowViewModel op = new DeckWindowViewModel(opdeckViewModel, opgameWatcher);
                opgameWatcher.Start("YuanShen.exe");
            }

            gameWatcher.Wait();
            opgameWatcher.Wait();
        }
    }
}