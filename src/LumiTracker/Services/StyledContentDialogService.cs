using LumiTracker.Config;
using LumiTracker.Controls;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace LumiTracker.Services
{
    public class StyledContentDialogService
    {
        private ContentDialogService MainService    = new ContentDialogService();
        private ContentDialogService ClosingService = new ContentDialogService();

        public void SetDialogHosts(ContentPresenter MainContentDialog, ContentPresenter ClosingContentDialog)
        {
            MainService.SetDialogHost(MainContentDialog);
            ClosingService.SetDialogHost(ClosingContentDialog);
        }

        public async Task<(ContentDialogResult, bool, bool)> ShowClosingDialogAsync()
        {
            // Init
            var content = new ClosingDialogContent();
            content.MinimizeButton.IsChecked     = (Configuration.Get<EClosingBehavior>("closing_behavior") == EClosingBehavior.Minimize);
            content.QuitButton.IsChecked         = !content.MinimizeButton.IsChecked;
            content.NotShowAgainButton.IsChecked = false;

            // Show Closing dialog
            var dialog = new ContentDialog()
            {
                Content = content,
                Title   = LocalizationSource.Instance["ClosingDialogTitle"],
                PrimaryButtonText = LocalizationSource.Instance["OK"],
                CloseButtonText   = LocalizationSource.Instance["Cancel"],
                PrimaryButtonIcon = new SymbolIcon(SymbolRegular.Checkmark24),
                CloseButtonIcon   = new SymbolIcon(SymbolRegular.Dismiss24),
                PrimaryButtonAppearance = ControlAppearance.Info,
            };
            ContentDialogResult result = await ClosingService.ShowAsync(dialog, default);

            bool MinimizeChecked = (content.MinimizeButton.IsChecked ?? false);
            bool NotShowAgainChecked = (content.NotShowAgainButton.IsChecked ?? false);

            return (result, MinimizeChecked, NotShowAgainChecked);
        }

        public async Task<(ContentDialogResult, string)> ShowTextInputDialogAsync(string title, string text, string placeholder)
        {
            // Init
            var content = new TextInputDialogContent();
            content.TextBox.Text = text;
            content.TextBox.PlaceholderText = placeholder;

            // Show TextBox dialog
            var dialog = new ContentDialog()
            {
                Content = content,
                Title   = title,
                PrimaryButtonText   = LocalizationSource.Instance["OK"],
                SecondaryButtonText = LocalizationSource.Instance["Paste"],
                CloseButtonText     = LocalizationSource.Instance["Cancel"],
                PrimaryButtonIcon   = new SymbolIcon(SymbolRegular.Checkmark24),
                SecondaryButtonIcon = new SymbolIcon(SymbolRegular.ClipboardPaste24),
                CloseButtonIcon     = new SymbolIcon(SymbolRegular.Dismiss24),
                PrimaryButtonAppearance = ControlAppearance.Info,
            };
            dialog.ButtonClicked += (ContentDialog sender, ContentDialogButtonClickEventArgs args) =>
            {
                if (args.Button == ContentDialogButton.Secondary)
                {
                    content.TextBox.Paste();
                }
            };
            dialog.Closing += (ContentDialog sender, ContentDialogClosingEventArgs args) =>
            {
                if (args.Result == ContentDialogResult.Secondary)
                {
                    args.Cancel = true;
                }
            };

            ContentDialogResult result = await MainService.ShowAsync(dialog, default);
            return (result, content.TextBox.Text);
        }

        public async Task<ContentDialogResult> ShowDeleteConfirmDialogAsync(string deckName)
        {
            // Init
            var content = new DeleteConfirmDialogContent();
            content.TextPrefix.Text = LocalizationSource.Instance["DeleteConfirm_Message"];
            content.TextMain.Text   = deckName;

            // Show dialog
            var dialog = new ContentDialog()
            {
                Title   = LocalizationSource.Instance["DeleteConfirm_Title"],
                Content = content,
                PrimaryButtonText = LocalizationSource.Instance["OK"],
                CloseButtonText   = LocalizationSource.Instance["Cancel"],
                PrimaryButtonIcon = new SymbolIcon(SymbolRegular.Delete24),
                CloseButtonIcon   = new SymbolIcon(SymbolRegular.Dismiss24),
                PrimaryButtonAppearance = ControlAppearance.Danger,
            };
            ContentDialogResult result = await MainService.ShowAsync(dialog, default);

            return result;
        }

        private ContentDialog? UpdateDialog;

        public async Task<Task<ContentDialogResult>?> ShowUpdateDialogAsync(UpdateContext ctx)
        {
            // Init latest release info dialog content
            var releaseContent = new LatestReleaseDialogContent();
            string title = ctx.ReleaseMeta!.tag_name;
            string text  = ctx.ReleaseMeta!.body;
            // Get update info of current language
            string[] textMultiLangs = text.Split("----------");
            if (textMultiLangs.Length == (int)ELanguage.NumELanguages - 1)
            {
                text = textMultiLangs[(int)Configuration.GetELanguage() - 1];
                text = text.TrimStart();
            }
            MarkdownParser.ParseMarkdown(releaseContent.RichTextBox.Document, text);

            // Show latest release info dialog
            UpdateDialog = new ContentDialog()
            {
                Title   = title,
                Content = releaseContent,
                PrimaryButtonText = LocalizationSource.Instance["UpdatePrompt_UpdateNow"],
                CloseButtonText   = LocalizationSource.Instance["UpdatePrompt_Later"],
                PrimaryButtonIcon = new SymbolIcon(SymbolRegular.Checkmark24),
                CloseButtonIcon   = new SymbolIcon(SymbolRegular.Dismiss24),
                PrimaryButtonAppearance = ControlAppearance.Success,
            };
            ContentDialogResult result = await MainService.ShowAsync(UpdateDialog, default);
            if (result == ContentDialogResult.None)
            {
                return null;
            }

            // Init progress dialog content
            var progressContent = new UpdateProgressDialogContent();
            var binding = new Binding(".")
            {
                Source = ctx,
                Mode = BindingMode.OneWay,
            };
            progressContent.SetBinding(UpdateProgressDialogContent.ContextProperty, binding);

            // Show progress dialog
            UpdateDialog = new ContentDialog()
            {
                Content = progressContent,
                CloseButtonText = LocalizationSource.Instance["OK"],
                CloseButtonIcon = new SymbolIcon(SymbolRegular.Checkmark24),
                CloseButtonAppearance = ControlAppearance.Success,
                IsPrimaryButtonEnabled   = false,
                IsSecondaryButtonEnabled = false,
                IsEnabled = false,
            };
            binding = new Binding("ProgressText")
            {
                Source = ctx,
                Mode = BindingMode.OneWay,
            };
            UpdateDialog.SetBinding(ContentDialog.TitleProperty, binding);
            binding = new Binding("ReadyToRestart")
            {
                Source = ctx,
                Mode = BindingMode.OneWay,
            };
            UpdateDialog.SetBinding(ContentDialog.IsEnabledProperty, binding);

            return MainService.ShowAsync(UpdateDialog, default);
        }

        public void ClearUpdateDialog()
        {
            if (UpdateDialog != null)
            {
                UpdateDialog.Hide();
                UpdateDialog = null;
            }
        }

        public async Task<ContentDialogResult> ShowCaptureTestDialogAsync(string filename, int width, int height)
        {
            // Init
            var content = new CaptureTestDialogContent();
            content.CapturedImage.Source = new BitmapImage(new Uri(Path.Combine(Configuration.LogDir, filename), UriKind.Absolute));

            // Show dialog
            var dialog = new ContentDialog()
            {
                Title   = $"{LocalizationSource.Instance["CaptureType_FrameSizePrompt"]} {width} x {height}",
                Content = content,
                CloseButtonText = LocalizationSource.Instance["OK"],
                CloseButtonIcon = new SymbolIcon(SymbolRegular.Checkmark24),
                CloseButtonAppearance    = ControlAppearance.Success,
                IsPrimaryButtonEnabled   = false,
                IsSecondaryButtonEnabled = false,
            };
            dialog.DialogMargin = new Thickness(0, -20, 0, -5);
            ContentDialogResult result = await MainService.ShowAsync(dialog, default);

            return result;
        }
    }
}
