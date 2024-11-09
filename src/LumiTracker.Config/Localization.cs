using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace LumiTracker.Config
{
    // https://stackoverflow.com/questions/50292087/dynamic-localized-wpf-application-with-resource-files
    public class LocalizationSource : INotifyPropertyChanged
    {
        private static readonly LocalizationSource instance = new LocalizationSource();

        public static LocalizationSource Instance
        {
            get { return instance; }
        }

        private readonly ResourceManager resManager = Lang.ResourceManager;

        public string this[string key]
        {
            get { return resManager.GetString(key, Lang.Culture)!; }
        }

        public CultureInfo CurrentCulture
        {
            get { return Lang.Culture; }
            set
            {
                if (Lang.Culture != value)
                {
                    Lang.Culture = value;
                    var @event = PropertyChanged;
                    if (@event != null)
                    {
                        @event.Invoke(this, new PropertyChangedEventArgs(string.Empty));
                    }
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
