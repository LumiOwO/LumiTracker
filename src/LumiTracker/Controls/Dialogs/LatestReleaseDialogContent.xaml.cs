using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace LumiTracker.Controls
{
    /// <summary>
    /// Interaction logic for LatestReleaseDialogContent.xaml
    /// </summary>
    public partial class LatestReleaseDialogContent : UserControl
    {
        public LatestReleaseDialogContent()
        {
            InitializeComponent();
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FlowDocument)
            {
                e.Handled = true;
            }
        }
    }
}
