using LumiTracker.Config;
using LumiTracker.Controls;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
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
            var styledDialog = new StyledContentDialog();
            styledDialog.Title = Lang.ClosingDialogTitle;
            var dialog = styledDialog.Dialog;
            dialog.Content = content;
            dialog.PrimaryButtonText = Lang.OK;
            dialog.CloseButtonText   = Lang.Cancel;
            dialog.PrimaryButtonIcon = new SymbolIcon(SymbolRegular.Checkmark24);
            dialog.CloseButtonIcon   = new SymbolIcon(SymbolRegular.Dismiss24);
            dialog.PrimaryButtonAppearance = ControlAppearance.Info;

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
            var styledDialog = new StyledContentDialog();
            styledDialog.Title = title;
            var dialog = styledDialog.Dialog;
            dialog.Content = content;
            dialog.PrimaryButtonText   = Lang.OK;
            dialog.SecondaryButtonText = Lang.Paste;
            dialog.CloseButtonText     = Lang.Cancel;
            dialog.PrimaryButtonIcon   = new SymbolIcon(SymbolRegular.Checkmark24);
            dialog.SecondaryButtonIcon = new SymbolIcon(SymbolRegular.ClipboardPaste24);
            dialog.CloseButtonIcon     = new SymbolIcon(SymbolRegular.Dismiss24);
            dialog.PrimaryButtonAppearance = ControlAppearance.Info;
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
            content.TextPrefix.Text = Lang.DeleteConfirm_Message;
            content.TextMain.Text   = deckName;

            // Show dialog
            var styledDialog = new StyledContentDialog();
            styledDialog.Title = Lang.DeleteConfirm_Title;
            var dialog = styledDialog.Dialog;
            dialog.Content = content;
            dialog.PrimaryButtonText = Lang.OK;
            dialog.CloseButtonText   = Lang.Cancel;
            dialog.PrimaryButtonIcon = new SymbolIcon(SymbolRegular.Delete24);
            dialog.CloseButtonIcon   = new SymbolIcon(SymbolRegular.Dismiss24);
            dialog.PrimaryButtonAppearance = ControlAppearance.Danger;

            ContentDialogResult result = await MainService.ShowAsync(dialog, default);
            return result;
        }

        private StyledContentDialog? UpdateDialog = null;

        private StyledContentDialog GetReleaseLogDialog(string title, string body, bool isStartup)
        {
            // Init
            var content = new LatestReleaseDialogContent();
            // Get update info of current language
            string[] textMultiLangs = body.Split("----------");
            if (textMultiLangs.Length == (int)ELanguage.NumELanguages - 1)
            {
                body = textMultiLangs[(int)Configuration.GetELanguage() - 1];
                body = body.TrimStart();
            }
            MarkdownParser.ParseMarkdown(content.RichTextBox.Document, body);

            // Show latest release info dialog
            var styledDialog = new StyledContentDialog();
            styledDialog.Title = title;
            var dialog = styledDialog.Dialog;
            dialog.Content = content;
            dialog.CloseButtonText   = Lang.UpdatePrompt_Later;
            dialog.CloseButtonIcon   = new SymbolIcon(SymbolRegular.Dismiss24);
            if (isStartup)
            {
                dialog.PrimaryButtonText = Lang.Donate_ButtonText;
                dialog.PrimaryButtonIcon = new SymbolIcon(SymbolRegular.Heart24);
                dialog.PrimaryButtonAppearance = ControlAppearance.Danger;
            }
            else
            {
                dialog.PrimaryButtonText = Lang.UpdatePrompt_UpdateNow;
                dialog.PrimaryButtonIcon = new SymbolIcon(SymbolRegular.Checkmark24);
                dialog.PrimaryButtonAppearance = ControlAppearance.Success;
            }

            return styledDialog;
        }

        public async Task<Task<ContentDialogResult>?> ShowUpdateDialogAsync(UpdateContext ctx)
        {
            // Init latest release info dialog content
            UpdateDialog = GetReleaseLogDialog(
                $"{Lang.LatestRelease_Title} : {ctx.ReleaseMeta!.tag_name}", ctx.ReleaseMeta!.body, false);
            ContentDialogResult result = await MainService.ShowAsync(UpdateDialog.Dialog, default);
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
            UpdateDialog = new StyledContentDialog();
            UpdateDialog.TitleCloseButtonVisibility = Visibility.Hidden;
            var dialog = UpdateDialog.Dialog;
            dialog.Content = progressContent;
            dialog.CloseButtonText = Lang.OK;
            dialog.CloseButtonIcon = new SymbolIcon(SymbolRegular.Checkmark24);
            dialog.CloseButtonAppearance = ControlAppearance.Success;
            dialog.IsPrimaryButtonEnabled   = false;
            dialog.IsSecondaryButtonEnabled = false;
            dialog.IsEnabled = false;
            binding = new Binding("ProgressText")
            {
                Source = ctx,
                Mode = BindingMode.OneWay,
            };
            UpdateDialog.SetBinding(StyledContentDialog.TitleProperty, binding);
            binding = new Binding("ReadyToRestart")
            {
                Source = ctx,
                Mode = BindingMode.OneWay,
            };
            dialog.SetBinding(ContentDialog.IsEnabledProperty, binding);

            return MainService.ShowAsync(dialog, default);
        }

        public void ClearUpdateDialog()
        {
            if (UpdateDialog != null)
            {
                UpdateDialog.Dialog.Hide();
                UpdateDialog = null;
            }
        }

        public async Task<ContentDialogResult> ShowCaptureTestDialogAsync(string filename, int width, int height)
        {
            // Init
            var content = new CaptureTestDialogContent();
            content.CapturedImage.Source = new BitmapImage(new Uri(Path.Combine(Configuration.LogDir, filename), UriKind.Absolute));

            // Show dialog
            var styledDialog = new StyledContentDialog();
            styledDialog.Title = $"{Lang.CaptureType_FrameSizePrompt} {width} x {height}";
            var dialog = styledDialog.Dialog;
            dialog.Content = content;
            dialog.CloseButtonText = Lang.OK;
            dialog.CloseButtonIcon = new SymbolIcon(SymbolRegular.Checkmark24);
            dialog.CloseButtonAppearance    = ControlAppearance.Success;
            dialog.IsPrimaryButtonEnabled   = false;
            dialog.IsSecondaryButtonEnabled = false;
            dialog.DialogMargin = new Thickness(0, -20, 0, -5);

            ContentDialogResult result = await MainService.ShowAsync(dialog, default);
            return result;
        }

        public async Task ShowDonateDialogAsync()
        {
            // Init
            var content = new DonateDialogContent();

            // Show dialog
            var styledDialog = new StyledContentDialog();
            styledDialog.Title = Lang.Donate;
            var dialog = styledDialog.Dialog;
            dialog.Content = content;
            dialog.CloseButtonText = Lang.UpdatePrompt_Later;
            dialog.CloseButtonIcon = new SymbolIcon(SymbolRegular.Dismiss24);
            dialog.IsPrimaryButtonEnabled = false;
            dialog.IsSecondaryButtonEnabled = false;
            dialog.DialogMargin = new Thickness(0, -20, 0, -5);

            ContentDialogResult result = await MainService.ShowAsync(dialog, default);
        }

        public async Task<ContentDialogResult> ShowWelcomeDialogAsync()
        {
            // Init
            var content = new WelcomeDialogContent();

            // Show dialog
            var styledDialog = new StyledContentDialog();
            styledDialog.Title = Lang.Welcome_Title;
            var dialog = styledDialog.Dialog;
            dialog.Content = content;
            dialog.PrimaryButtonText = Lang.Donate_ButtonText;
            dialog.PrimaryButtonIcon = new SymbolIcon(SymbolRegular.Heart24);
            dialog.PrimaryButtonAppearance = ControlAppearance.Danger;
            dialog.CloseButtonText = Lang.UpdatePrompt_Later;
            dialog.CloseButtonIcon = new SymbolIcon(SymbolRegular.Dismiss24);
            dialog.IsSecondaryButtonEnabled = false;
            dialog.DialogMargin = new Thickness(0, -20, 0, -5);

            ContentDialogResult result = await MainService.ShowAsync(dialog, default);
            return result;
        }

        private async Task _ShowStartupDialogIfNeededAsync()
        {
            Guid guid = Configuration.GetOrCreateGuid(out bool guidNewlyCreated);
            bool just_updated = Configuration.Get<bool>("just_updated");

            if (!guidNewlyCreated && just_updated)
            {
                StyledContentDialog? styledDialog = null;
                try
                {
                    string version = Configuration.GetAssemblyVersion();
                    string changeLogPath = Path.Combine(Configuration.ChangeLogDir, $"{version}.md");
                    if (File.Exists(changeLogPath))
                    {
                        string body = File.ReadAllText(changeLogPath);
                        styledDialog = GetReleaseLogDialog($"{Lang.CurrentVersion_Title} : v{version}", body, true);
                    }
                    else
                    {
                        Configuration.Logger.LogWarning($"[ShowStartupDialog] Just updated, but release log file not found.");
                    }
                }
                catch (Exception ex)
                { 
                    Configuration.Logger.LogError($"[ShowStartupDialog] Failed to read release log: {ex.Message}");
                }

                if (styledDialog != null)
                {
                    ContentDialogResult result = await MainService.ShowAsync(styledDialog.Dialog, default);
                    if (result == ContentDialogResult.Primary)
                    {
                        await ShowDonateDialogAsync();
                    }
                    return;
                }
            }

            if (just_updated || guidNewlyCreated)
            {
                ContentDialogResult result = await ShowWelcomeDialogAsync();
                if (result == ContentDialogResult.Primary)
                {
                    await ShowDonateDialogAsync();
                }
            }
        }

        public async Task ShowStartupDialogIfNeededAsync(Func<Task> OnDialogClosed)
        {
            await _ShowStartupDialogIfNeededAsync();
            await OnDialogClosed();
        }
    }
}
