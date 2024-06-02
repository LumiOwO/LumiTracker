using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace LumiTracker.Controls
{
    public class HeaderFilledTabControl : TabControl
    {
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            double newW = (ActualWidth / Items.Count) - 2;
            newW = Math.Max(0, newW);

            foreach (TabItem item in Items)
            {
                item.Width = newW;
            }
        }
    }
}
