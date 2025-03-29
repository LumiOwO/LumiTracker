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

        public ERegionType RegionType { get; set; } = ERegionType.GameStart;
        public int CharacterIndex { get; set; } = 0;
    }

    public partial class CanvasWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private LocalizationTextItem _canvasWindowTitle = new ();

        [ObservableProperty]
        private ObservableCollection<OverlayElement> _elements = new ();

        private int Width { get; set; } = 0;

        private int Height { get; set; } = 0;

        private float DpiScale { get; set; } = 1.0f;

        private ERatioType RatioType { get; set; } = ERatioType.E16_9;

        public CanvasWindowViewModel()
        {
            var binding = LocalizationExtension.Create("CanvasWindowTitle");
            binding.Converter = new OverlayWindowTitleNameConverter();
            BindingOperations.SetBinding(CanvasWindowTitle, LocalizationTextItem.TextProperty, binding);

            // Debug: display task region on client rect
            //AddElement(new OverlayElement("debug")
            //{
            //    Background = Brushes.Magenta,
            //    Opacity = 0.3,
            //    RegionType = ERegionType.CharVS,
            //    CharacterIndex = 5,
            //});
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

        public void ResizeAllElements(int client_width, int client_height, float dpiScale)
        {
            if (!RegionUtils.Loaded) return;

            Width    = client_width; 
            Height   = client_height; 
            DpiScale = dpiScale;

            var ratioType = RegionUtils.GetRatioType(client_width, client_height);
            RatioType = ratioType;

            float dpiScaleInv = 1.0f / dpiScale;
            foreach (var element in Elements)
            {
                var regionType = element.RegionType;
                var box = RegionUtils.Get(ratioType, regionType);
                // default: (left, top, width, height)
                double left   = Math.Round(client_width  * box[0]) * dpiScaleInv;
                double top    = Math.Round(client_height * box[1]) * dpiScaleInv;
                double width  = Math.Round(client_width  * box[2]) * dpiScaleInv;
                double height = Math.Round(client_height * box[3]) * dpiScaleInv;

                if (regionType == ERegionType.CharVS)
                {
                    box = RegionUtils.Get(ratioType, ERegionType.CharOffset);
                    double margin = Math.Round(client_width * box[0]) * dpiScaleInv;

                    double offset;
                    if (element.CharacterIndex < 3)
                    {
                        offset = element.CharacterIndex * (width + margin);
                    }
                    else
                    {
                        double op_left = client_width * dpiScaleInv - (left + width + 2 * (width + margin));
                        offset = op_left - left;
                        offset += (element.CharacterIndex - 3) * (width + margin);
                    }
                    element.Position = new Rect(left + offset, top, width, height);
                }
                else
                {
                    // Some regions are anchored by other detections (e.g., FlowAnchor and CharOffset)
                    // Displaying these regions on screen may produce unexpected visual results
                    element.Position = new Rect(left, top, width, height);
                }
            }
        }
    }
}
