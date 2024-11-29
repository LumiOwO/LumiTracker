using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace LumiTracker.Services
{
    public class ApplicationThemeService
    {
        public static void ChangeThemeTo(ApplicationTheme theme)
        {
            // Overwrite accent color
            ApplicationAccentColorManager.Apply(
                Color.FromArgb(0xff, 0x19, 0xc4, 0xcf), theme, false);
            // refresh theme; do not use system accent!
            ApplicationThemeManager.Apply(theme, updateAccent: false);

            // dynamic resources
            var GlobalRes = Application.Current.Resources;
            if (theme == ApplicationTheme.Light)
            {
                GlobalRes["DeckPageBaseBackground"]              = new SolidColorBrush(Color.FromArgb(0xff, 0xee, 0xee, 0xee));
                GlobalRes["DeckPageDeckTitleBloomBrush"]         = new SolidColorBrush(Color.FromArgb(0xff, 0x8c, 0x6a, 0x4a));
                GlobalRes["CardListBackground"]                  = new SolidColorBrush(Color.FromArgb(0xff, 0xf4, 0xf4, 0xf4));
                GlobalRes["DeckPageMatchupsBorderBrush"]         = new SolidColorBrush(Color.FromArgb(0xff, 0x60, 0x60, 0x60));
                GlobalRes["TabViewItemHeaderBackgroundSelected"] = new SolidColorBrush(Color.FromArgb(0xff, 0xf9, 0xf9, 0xf9));
            }
            else if (theme == ApplicationTheme.Dark)
            {
                GlobalRes["DeckPageBaseBackground"]              = new SolidColorBrush(Color.FromArgb(0xff, 0x27, 0x27, 0x27));
                GlobalRes["DeckPageDeckTitleBloomBrush"]         = new SolidColorBrush(Color.FromArgb(0xff, 0x82, 0xb1, 0xff));
                GlobalRes["CardListBackground"]                  = new SolidColorBrush(Color.FromArgb(0xff, 0x28, 0x2c, 0x34));
                GlobalRes["DeckPageMatchupsBorderBrush"]         = new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0xff, 0xff));
                GlobalRes["TabViewItemHeaderBackgroundSelected"] = new SolidColorBrush(Color.FromArgb(0xff, 0x27, 0x2c, 0x34));
            }
        }
    }
}
