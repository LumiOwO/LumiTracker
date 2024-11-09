using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace LumiTracker.Controls
{
    /// <summary>
    /// Interaction logic for StyledContentDialog.xaml
    /// </summary>
    public partial class StyledContentDialog : UserControl
    {
        public StyledContentDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            "Title", typeof(string), typeof(StyledContentDialog), new PropertyMetadata(""));

        public string Title
        {
            get { return (GetValue(TitleProperty) as string)!; }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty TitleCloseButtonVisibilityProperty = DependencyProperty.Register(
            "TitleCloseButtonVisibility", typeof(Visibility), typeof(StyledContentDialog), new PropertyMetadata(Visibility.Visible));

        public Visibility TitleCloseButtonVisibility
        {
            get { return (Visibility)GetValue(TitleCloseButtonVisibilityProperty); }
            set { SetValue(TitleCloseButtonVisibilityProperty, value); }
        }

        [RelayCommand]
        public void OnCloseContentDialog()
        {
            Dialog.Hide(ContentDialogResult.None);
        }
    }
}
