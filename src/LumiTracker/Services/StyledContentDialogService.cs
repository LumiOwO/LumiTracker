using LumiTracker.Config;
using LumiTracker.Controls;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Windows.Controls;
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
                PrimaryButtonIcon = new SymbolIcon(SymbolRegular.CheckmarkCircle24),
                CloseButtonIcon   = new SymbolIcon(SymbolRegular.DismissCircle24),
            };
            ContentDialogResult result = await ClosingService.ShowAsync(dialog, default);

            bool MinimizeChecked = (content.MinimizeButton.IsChecked ?? false);
            bool NotShowAgainChecked = (content.NotShowAgainButton.IsChecked ?? false);

            return (result, MinimizeChecked, NotShowAgainChecked);
        }

        public async Task<(ContentDialogResult, string)> ShowTextInputDialogAsync(string title, string text, string placeholder)
        {
            // Init
            var content = new TextInputDialogDialogContent();
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
                PrimaryButtonIcon   = new SymbolIcon(SymbolRegular.CheckmarkCircle24),
                SecondaryButtonIcon = new SymbolIcon(SymbolRegular.ClipboardPaste24),
                CloseButtonIcon     = new SymbolIcon(SymbolRegular.DismissCircle24),
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

        public async Task<ContentDialogResult> ShowSimpleDialogAsync(string title)
        {
            // Show dialog
            ContentDialogResult result = await MainService.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions()
                {
                    Content = "",
                    Title = title,
                    PrimaryButtonText = LocalizationSource.Instance["OK"],
                    CloseButtonText = LocalizationSource.Instance["Cancel"],
                }
            );
            return result;
        }
    }
}
