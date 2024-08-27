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
            DeckViewModel deckViewModel = new DeckViewModel(null, null);
            GameWatcher gameWatcher = new GameWatcher();
            DeckWindowViewModel my = new DeckWindowViewModel(deckViewModel, gameWatcher);
            using (Configuration.Logger.BeginScope(new ScopeState { Name = "MY", Color = LogHelper.AnsiOrange }))
            {
                gameWatcher.Start("YuanShen.exe");
            }

            // my model
            DeckViewModel opdeckViewModel = new DeckViewModel(null, null);
            GameWatcher opgameWatcher = new GameWatcher();
            DeckWindowViewModel op = new DeckWindowViewModel(opdeckViewModel, opgameWatcher);
            using (Configuration.Logger.BeginScope(new ScopeState { Name = "OP", Color = LogHelper.AnsiBlue }))
            {
                opgameWatcher.Start("YuanShen.exe");
            }

            gameWatcher.Wait();
            opgameWatcher.Wait();
        }
    }
}