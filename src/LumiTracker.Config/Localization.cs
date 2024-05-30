using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

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
        private CultureInfo? currentCulture;

        public string this[string key]
        {
            get { return this.resManager.GetString(key, this.currentCulture)!; }
        }

        public CultureInfo CurrentCulture
        {
            get { return this.currentCulture!; }
            set
            {
                if (this.currentCulture != value)
                {
                    this.currentCulture = value;
                    var @event = this.PropertyChanged;
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
