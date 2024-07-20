using LumiTracker.ViewModels.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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
