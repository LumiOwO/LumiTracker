using System.Windows.Controls;

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
    }
}
