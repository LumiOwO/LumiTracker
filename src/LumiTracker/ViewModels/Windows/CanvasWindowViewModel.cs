using System.Collections.ObjectModel;

using LumiTracker.Config;
using LumiTracker.Services;
using System.Windows.Data;
using LumiTracker.Helpers;
using System.Windows.Media;

namespace LumiTracker.ViewModels.Windows
{
    public partial class OverlayElement(string name) : ObservableObject
    {
        [ObservableProperty]
        public string _elementName = name;
        [ObservableProperty]
        public Rect _position = new Rect(0, 0, 0, 0);
        [ObservableProperty]
        public Uri? _imageSource = null;
        [ObservableProperty]
        private Brush _background = Brushes.Transparent;
        [ObservableProperty]
        public double _opacity = 1.0;
        [ObservableProperty]
        public CornerRadius? _cornerRadius = null;
        [ObservableProperty]
        public Thickness? _borderThickness = null;
    }

    public partial class CanvasWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private LocalizationTextItem _canvasWindowTitle = new ();

        [ObservableProperty]
        private ObservableCollection<OverlayElement> _elements = new ();

        [ObservableProperty]
        private int _width = 0;

        [ObservableProperty]
        private int _height = 0;

        [ObservableProperty]
        private float _dpiScale = 1.0f;

        public CanvasWindowViewModel()
        {
            var binding = LocalizationExtension.Create("DeckWindowTitle"); // TODO: add CanvasWindowTitle
            binding.Converter = new OverlayWindowTitleNameConverter();
            BindingOperations.SetBinding(CanvasWindowTitle, LocalizationTextItem.TextProperty, binding);

            // TODO: remove test
            AddElement(new OverlayElement("test")
            {
                Background = Brushes.Red,
                Opacity = 0.3,
            });
        }

        public void AddElement(OverlayElement element)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Elements.Add(element);
            });
        }

        public void RemoveElement(string name)
        {
            var element = Elements.FirstOrDefault(e => e.ElementName == name);
            if (element != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Elements.Remove(element);
                });
            }
        }

        public void ResizeAllElements(int width, int height, float dpiScale)
        {
            Width = width; 
            Height = height; 
            DpiScale = dpiScale;

            // TODO: remove test
            foreach (var element in Elements)
            {
                element.Position = new Rect(0, 0, 0.5 * width / DpiScale, 0.5 * height / DpiScale);
            }
        }
    }
}
