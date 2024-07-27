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

        public DeckWindowTabHeader()
        {
            InitializeComponent();
        }
    }
}
