using LumiTracker.Config;
using LumiTracker.Controls;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

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
            // Init
            var content = new DeleteConfirmDialogContent();
            content.TextPrefix.Text = LocalizationSource.Instance["DeleteConfirm_Message"];

            // Show dialog
            UpdateDialog = new ContentDialog()
            {
                Title = LocalizationSource.Instance["DeleteConfirm_Title"],
                PrimaryButtonText = LocalizationSource.Instance["OK"],
                CloseButtonText = LocalizationSource.Instance["Cancel"],
                PrimaryButtonIcon = new SymbolIcon(SymbolRegular.Delete24),
                CloseButtonIcon = new SymbolIcon(SymbolRegular.Dismiss24),
                PrimaryButtonAppearance = ControlAppearance.Danger,
            };
            ContentDialogResult result = await MainService.ShowAsync(UpdateDialog, default);
            if (result == ContentDialogResult.None)
            {
                return null;
            }

            // Show dialog
            UpdateDialog = new ContentDialog()
            {
                Title = LocalizationSource.Instance["DeleteConfirm_Title"],
                Content = "",
                IsFooterVisible = false,
            };
            var binding = new Binding("ReadyToRestart")
            {
                Source = ctx,
                Mode   = BindingMode.OneWay,
            };
            UpdateDialog.SetBinding(ContentDialog.IsFooterVisibleProperty, binding);

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
    }
}
