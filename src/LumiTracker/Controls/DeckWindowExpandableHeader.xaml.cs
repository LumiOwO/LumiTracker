﻿using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LumiTracker.Controls
{
    public partial class DeckWindowExpandableHeader : UserControl
    {
        public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
            "IsExpanded", typeof(bool), typeof(DeckWindowExpandableHeader), new PropertyMetadata(false));

        public bool IsExpanded
        {
            get { return (bool)GetValue(IsExpandedProperty); }
            set { SetValue(IsExpandedProperty, value); }
        }

        public static readonly DependencyProperty HeaderMarginProperty = DependencyProperty.Register(
            "HeaderMargin", typeof(Thickness), typeof(DeckWindowExpandableHeader), new PropertyMetadata(new Thickness(0)));

        public Thickness HeaderMargin
        {
            get { return (Thickness)GetValue(HeaderMarginProperty); }
            set { SetValue(HeaderMarginProperty, value); }
        }

        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
            "Header", typeof(string), typeof(DeckWindowExpandableHeader), new PropertyMetadata(""));

        public string Header
        {
            get { return (GetValue(HeaderProperty) as string)!; }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty HeaderFontSizeProperty = DependencyProperty.Register(
            "HeaderFontSize", typeof(double), typeof(DeckWindowExpandableHeader), new PropertyMetadata(1.0));

        public double HeaderFontSize
        {
            get { return (double)GetValue(HeaderFontSizeProperty); }
            set { SetValue(HeaderFontSizeProperty, value); }
        }

        public static readonly DependencyProperty CountProperty = DependencyProperty.Register(
            "Count", typeof(int), typeof(DeckWindowExpandableHeader), new PropertyMetadata(0));

        public int Count
        {
            get { return (int)GetValue(CountProperty); }
            set { SetValue(CountProperty, value); }
        }

        public static readonly RoutedEvent IsExpandedChangedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(IsExpandedChanged),
                RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<bool>),
                typeof(DeckWindowExpandableHeader)
            );

        // Expose the event for subscription
        public event RoutedPropertyChangedEventHandler<bool> IsExpandedChanged
        {
            add => AddHandler(IsExpandedChangedEvent, value);
            remove => RemoveHandler(IsExpandedChangedEvent, value);
        }

        public DeckWindowExpandableHeader()
        {
            InitializeComponent();
        }

        private void OnIsExpandedChanged(object sender, RoutedEventArgs e)
        {
            bool isChecked = Expander.IsChecked ?? false;
            var args = new RoutedPropertyChangedEventArgs<bool>(
                !isChecked, // Old value (toggle inverse for demo)
                isChecked,  // New value
                IsExpandedChangedEvent
            );
            RaiseEvent(args);
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            MainBorder.Background = (Brush)Resources["SubtleFillColorSecondaryBrush"];
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            MainBorder.Background = (Brush)Resources["HeaderBackground"];
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source != Expander)
            {
                IsExpanded = !IsExpanded;
            }
        }
    }
}
