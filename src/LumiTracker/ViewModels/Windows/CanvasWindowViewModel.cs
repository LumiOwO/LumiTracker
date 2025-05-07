using System.Collections.ObjectModel;

using LumiTracker.Config;
using LumiTracker.Services;
using System.Windows.Data;
using LumiTracker.Helpers;
using System.Windows.Media;
using LumiTracker.Watcher;

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
        public bool IsActiveCharacter { get; set; } = false;
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

        private GameEventHook _hook;

        public CanvasWindowViewModel(GameEventHook hook)
        {
            var binding = LocalizationExtension.Create("CanvasWindowTitle");
            binding.Converter = new OverlayWindowTitleNameConverter();
            BindingOperations.SetBinding(CanvasWindowTitle, LocalizationTextItem.TextProperty, binding);

            _hook = hook;
            _hook.ActiveIndices += OnActiveIndices;

            // Debug: display task region on client rect
            // for (int i = 0; i < 6; i++)
            // {
            //     AddElement(new OverlayElement($"char{i}")
            //     {
            //        Background = Brushes.Magenta,
            //        Opacity = 0.2,
            //        RegionType = ERegionType.CharInGame,
            //        CharacterIndex = i
            //     });
            // }
        }

        private void OnActiveIndices(int my_index, int op_index)
        {
            // Debug
            for (int i = 0; i < 6; i++)
            {
                var element = GetElement($"char{i}");
                if (element == null) continue;
                element.IsActiveCharacter = i < 3 ? (my_index == i) : (op_index == i);
                element.Position = ComputeRegionRect(element);
            }
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

        public OverlayElement? GetElement(string name)
        {
            return Elements.FirstOrDefault(e => e.ElementName == name);
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
                element.Position = ComputeRegionRect(element);
            }
        }

        private Rect ComputeRegionRect(OverlayElement element)
        {
            return element.RegionType switch
            {
                ERegionType.CharVS     => ComputeRegionRectCharVS(element),
                ERegionType.CharInGame => ComputeRegionRectCharInGame(element),
                ERegionType.CharBorder => ComputeRegionRectCharBorder(element),
                // Some regions are anchored by other detections (e.g., FlowAnchor, CharBorder, CharMargin)
                // Displaying these regions on screen may produce unexpected visual results
                _ => ComputeRegionRectDefault(element),
            };
        }

        private Rect ComputeRegionRectCharVS(OverlayElement element)
        {
            var box = RegionUtils.Get(RatioType, ERegionType.CharVS);
            float dpiScaleInv = 1.0f / DpiScale;
            double left   = Math.Round(Width  * box.x) * dpiScaleInv;
            double top    = Math.Round(Height * box.y) * dpiScaleInv;
            double width  = Math.Round(Width  * box.z) * dpiScaleInv;
            double height = Math.Round(Height * box.w) * dpiScaleInv;
            box = RegionUtils.Get(RatioType, ERegionType.CharMargin);
            double margin = Math.Round(Width  * box.x) * dpiScaleInv;

            double offset;
            if (element.CharacterIndex < 3)
            {
                offset = element.CharacterIndex * (width + margin);
            }
            else
            {
                double op_left = Width * dpiScaleInv - (left + width + 2 * (width + margin));
                offset = op_left - left;
                offset += (element.CharacterIndex - 3) * (width + margin);
            }

            return new Rect(left + offset, top, width, height);
        }

        private Rect ComputeRegionRectCharInGame(OverlayElement element)
        {
            var box = RegionUtils.Get(RatioType, ERegionType.CharInGame);
            float dpiScaleInv = 1.0f / DpiScale;
            double left   = Math.Round(Width  * box.x) * dpiScaleInv;
            double top    = Math.Round(Height * box.y) * dpiScaleInv;
            double width  = Math.Round(Width  * box.z) * dpiScaleInv;
            double height = Math.Round(Height * box.w) * dpiScaleInv;
            box = RegionUtils.Get(RatioType, ERegionType.CharMargin);
            double margin = Math.Round(Width  * box.y) * dpiScaleInv;
            double deltaY = Math.Round(Height * box.z) * dpiScaleInv;

            double offsetX = (element.CharacterIndex % 3) * (width + margin);
            double offsetY = 0;
            if (element.CharacterIndex >= 3)
            {
                double op_top = Height * dpiScaleInv - (top + height);
                offsetY = op_top - top;
            }
            if (element.IsActiveCharacter)
            {
                offsetY += deltaY * (element.CharacterIndex < 3 ? -1 : 1);
            }

            return new Rect(left + offsetX, top + offsetY, width, height);
        }

        private Rect ComputeRegionRectCharBorder(OverlayElement element)
        {
            Rect rect = ComputeRegionRectCharInGame(element);
            var box = RegionUtils.Get(RatioType, ERegionType.CharBorder);
            double left   = Math.Round(rect.Width  * (1.0f - box.x) * 0.5);
            double width  = Math.Round(rect.Width  * box.x);
            double height = Math.Round(rect.Height * box.y);
            return new Rect(rect.Left + left, rect.Top, width, height);
        }

        private Rect ComputeRegionRectDefault(OverlayElement element)
        {
            // default: (left, top, width, height)
            var box = RegionUtils.Get(RatioType, element.RegionType);
            float dpiScaleInv = 1.0f / DpiScale;
            double left   = Math.Round(Width  * box.x) * dpiScaleInv;
            double top    = Math.Round(Height * box.y) * dpiScaleInv;
            double width  = Math.Round(Width  * box.z) * dpiScaleInv;
            double height = Math.Round(Height * box.w) * dpiScaleInv;

            return new Rect(left, top, width, height);
        }
    }
}
