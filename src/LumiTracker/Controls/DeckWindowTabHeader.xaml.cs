using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace LumiTracker.Controls
{
    public partial class DeckWindowTabHeader : UserControl
    {
        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            "Icon", typeof(SymbolRegular), typeof(DeckWindowTabHeader), new PropertyMetadata(null));

        public SymbolRegular Icon
        {
            get { return (SymbolRegular)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            "Text", typeof(string), typeof(DeckWindowTabHeader), new PropertyMetadata(null));

        public string Text
        {
            get { return (GetValue(TextProperty) as string)!; }
            set { SetValue(TextProperty, value); }
        }

        public static readonly new DependencyProperty FontSizeProperty = DependencyProperty.Register(
            "FontSize", typeof(double), typeof(DeckWindowTabHeader), new PropertyMetadata(1.0));

        public new double FontSize
        {
            get { return (double)GetValue(FontSizeProperty); }
            set { SetValue(FontSizeProperty, value); }
        }

        public static readonly DependencyProperty IconMarginProperty = DependencyProperty.Register(
            "IconMargin", typeof(Thickness), typeof(DeckWindowTabHeader), new PropertyMetadata(new Thickness(0)));

        public Thickness IconMargin
        {
            get { return (Thickness)GetValue(IconMarginProperty); }
            set { SetValue(IconMarginProperty, value); }
        }

        public static readonly DependencyProperty TextMarginProperty = DependencyProperty.Register(
            "TextMargin", typeof(Thickness), typeof(DeckWindowTabHeader), new PropertyMetadata(new Thickness(0)));

        public Thickness TextMargin
        {
            get { return (Thickness)GetValue(TextMarginProperty); }
            set { SetValue(TextMarginProperty, value); }
        }

        public DeckWindowTabHeader()
        {
            InitializeComponent();
        }
    }
}
