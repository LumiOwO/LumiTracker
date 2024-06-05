using System.Windows.Media;
using Wpf.Ui.Controls;
using LumiTracker.Config;

namespace LumiTracker.ViewModels.Pages
{
    public struct DataColor
    {
        public Brush Color { get; set; }
    }

    public partial class AboutViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;

        [ObservableProperty]
        private IEnumerable<DataColor> _colors = new List<DataColor>();


        [ObservableProperty]
        private string _appVersion = "";


        public void OnNavigatedTo()
        {
            if (!_isInitialized)
                InitializeViewModel();
        }

        public void OnNavigatedFrom() { }

        private string GetAssemblyVersion()
        {
            Version? version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "";
        }

        private void InitializeViewModel()
        {
            AppVersion = $"{LocalizationSource.Instance["AppName"]} v{GetAssemblyVersion()}";

            var random = new Random();
            var colorCollection = new List<DataColor>();

            for (int i = 0; i < 8192; i++)
                colorCollection.Add(
                    new DataColor
                    {
                        Color = new SolidColorBrush(
                            Color.FromArgb(
                                (byte)200,
                                (byte)random.Next(0, 250),
                                (byte)random.Next(0, 250),
                                (byte)random.Next(0, 250)
                            )
                        )
                    }
                );

            Colors = colorCollection;

            _isInitialized = true;
        }
    }
}
