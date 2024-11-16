using LumiTracker.Config;
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
        public string Key
        {
            get => Path.Path.TrimStart('[').TrimEnd(']');
            set => Path = new PropertyPath($"[{value}]");
        }

        public LocalizationExtension(string key) : base("[" + key + "]")
        {
            Mode   = BindingMode.OneWay;
            Source = LocalizationSource.Instance;
        }

        public LocalizationExtension() : this("None")
        {

        }

        public static LocalizationExtension Create(string key = "None")
        {
            return new LocalizationExtension(key);
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
    }

    public class LocalizationService : ILocalizationService
    {
        public void ChangeLanguage(string? lang = null)
        {
            lang = EnumHelpers.ParseLanguageName(lang);
            LocalizationSource.Instance.CurrentCulture = new CultureInfo(lang);
        }
    }
}
