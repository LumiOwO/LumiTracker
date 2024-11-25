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
        public static readonly DependencyProperty ShowDonationPromptProperty = DependencyProperty.Register(
            "ShowDonationPrompt", typeof(bool), typeof(LatestReleaseDialogContent), new PropertyMetadata(true));

        public bool ShowDonationPrompt
        {
            get { return (bool)GetValue(ShowDonationPromptProperty); }
            set { SetValue(ShowDonationPromptProperty, value); }
        }

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
