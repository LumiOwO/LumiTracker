using LumiTracker.Config;
using Microsoft.Win32;
using System.Globalization;
using System.Windows.Data;

namespace LumiTracker.Services
{
    public class LocalizationTextItem : DependencyObject
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            "Text", typeof(string), typeof(LocalizationTextItem), new PropertyMetadata(""));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }
    }

    public class LocalizationExtension : Binding
    {
        public LocalizationExtension(string name) : base("[" + name + "]")
        {
            Mode   = BindingMode.OneWay;
            Source = LocalizationSource.Instance;
        }

        public static LocalizationExtension Create(string name = "None")
        {
            return new LocalizationExtension(name);
        }

        public static LocalizationExtension Create<T>(T value) where T : Enum
        {
            string typeStr = value!.GetType().Name.Substring(1);
            string valStr  = value.ToString()!;
            return new LocalizationExtension($"{typeStr}_{valStr}");
        }
    }

    public interface ILocalizationService
    {
        void ChangeLanguage(string lang);
        void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e);
    }

    public class LocalizationService : ILocalizationService
    {
        public void ChangeLanguage(string? lang = null)
        {
            lang = EnumHelpers.ParseLanguageName(lang);
            LocalizationSource.Instance.CurrentCulture = new CultureInfo(lang);
        }

        public void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.Locale)
            {
                if (Configuration.IsLanguageFollowSystem())
                {
                    string lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                    ChangeLanguage(lang);
                }
            }
        }
    }
}
