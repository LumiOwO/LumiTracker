using Wpf.Ui.Controls;
using LumiTracker.Config;
using Microsoft.Extensions.Logging;
using LumiTracker.Models;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using System.Windows.Media;
using System.IO;
using System.Windows.Input;
using LumiTracker.Services;
using System.Windows.Data;
using Newtonsoft.Json.Linq;
using Wpf.Ui;

namespace LumiTracker.OB.ViewModels.Pages
{
    public partial class DuelViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;

        public DuelViewModel()
        {
            
        }

        public void OnNavigatedTo()
        {
            if (!_isInitialized)
                InitializeViewModel();
        }

        public void OnNavigatedFrom() 
        { 
        }

        private void InitializeViewModel() 
        { 
        }

    }
}
