using LumiTracker.Services;
using System.Windows.Controls;

namespace LumiTracker.Controls
{
    /// <summary>
    /// Interaction logic for UpdateProgressDialogContent.xaml
    /// </summary>
    public partial class UpdateProgressDialogContent : UserControl
    {
        public static readonly DependencyProperty ContextProperty = DependencyProperty.Register(
            "Context", typeof(UpdateContext), typeof(UpdateProgressDialogContent), new PropertyMetadata(null));

        public UpdateContext Context
        {
            get { return (UpdateContext)GetValue(ContextProperty); }
            set { SetValue(ContextProperty, value); }
        }

        public UpdateProgressDialogContent()
        {
            InitializeComponent();
        }
    }
}
