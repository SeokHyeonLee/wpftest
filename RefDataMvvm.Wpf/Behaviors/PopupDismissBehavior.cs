using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace RefDataMvvm.Wpf.Behaviors
{
    // View-only input adapter. State changes always go through the bound ICommand.
    // StaysOpen=True avoids Popup's mouse capture swallowing toolbar/dim input.
    public static class PopupDismissBehavior
    {
        public static readonly DependencyProperty CloseCommandProperty = DependencyProperty.RegisterAttached(
            "CloseCommand", typeof(ICommand), typeof(PopupDismissBehavior),
            new PropertyMetadata(null, OnCloseCommandChanged));

        public static readonly DependencyProperty KeepOpenElementProperty = DependencyProperty.RegisterAttached(
            "KeepOpenElement", typeof(FrameworkElement), typeof(PopupDismissBehavior));

        private static readonly DependencyProperty SubscriptionProperty = DependencyProperty.RegisterAttached(
            "Subscription", typeof(Subscription), typeof(PopupDismissBehavior));

        public static void SetCloseCommand(DependencyObject target, ICommand value) { target.SetValue(CloseCommandProperty, value); }
        public static ICommand GetCloseCommand(DependencyObject target) { return (ICommand)target.GetValue(CloseCommandProperty); }
        public static void SetKeepOpenElement(DependencyObject target, FrameworkElement value) { target.SetValue(KeepOpenElementProperty, value); }
        public static FrameworkElement GetKeepOpenElement(DependencyObject target) { return (FrameworkElement)target.GetValue(KeepOpenElementProperty); }

        private static void OnCloseCommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            var popup = target as Popup;
            if (popup == null) throw new ArgumentException("CloseCommand must be attached to a Popup.");
            var previous = (Subscription)popup.GetValue(SubscriptionProperty);
            if (previous != null) previous.Dispose();
            // DataContext can disappear before Unloaded when the view is removed.
            // Reset the outgoing VM through its old command before releasing it.
            var oldCommand = e.OldValue as ICommand;
            if (e.NewValue == null && oldCommand != null && oldCommand.CanExecute(null))
                oldCommand.Execute(null);
            popup.SetValue(SubscriptionProperty, e.NewValue == null ? null : new Subscription(popup));
        }

        private static bool IsWithin(DependencyObject source, DependencyObject ancestor)
        {
            if (ancestor == null) return false;
            for (var current = source; current != null;)
            {
                if (ReferenceEquals(current, ancestor)) return true;
                if (current is Visual || current is Visual3D)
                    current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
                else
                    current = (current as FrameworkContentElement)?.Parent ?? LogicalTreeHelper.GetParent(current);
            }
            return false;
        }

        private sealed class Subscription : IDisposable
        {
            private readonly Popup _popup;
            private readonly MouseButtonEventHandler _mouseDown;
            private readonly RoutedEventHandler _click;
            private Window _window;
            private UIElement _child;

            public Subscription(Popup popup)
            {
                _popup = popup;
                _mouseDown = OnMouseDown;
                _click = OnClick;
                popup.Opened += OnOpened;
                popup.Closed += OnClosed;
                popup.Loaded += OnLoaded;
                popup.Unloaded += OnUnloaded;
                if (popup.IsOpen) Attach();
            }

            private void OnOpened(object sender, EventArgs e) { Attach(); }
            private void OnClosed(object sender, EventArgs e) { Detach(); }
            private void OnLoaded(object sender, RoutedEventArgs e) { if (_popup.IsOpen) Attach(); }
            private void OnUnloaded(object sender, RoutedEventArgs e) { Close(); Detach(); }

            private void Attach()
            {
                Detach();
                _window = Window.GetWindow(_popup.PlacementTarget ?? _popup);
                _child = _popup.Child;
                if (_window != null)
                {
                    _window.AddHandler(Mouse.PreviewMouseDownEvent, _mouseDown, true);
                    _window.AddHandler(ButtonBase.ClickEvent, _click, true);
                    _window.Closed += OnOwnerClosed;
                }
                // Popup has a separate presentation source: its Click route does not reach Window.
                if (_child != null) _child.AddHandler(ButtonBase.ClickEvent, _click, true);
            }

            private void OnMouseDown(object sender, MouseButtonEventArgs e)
            {
                var source = e.OriginalSource as DependencyObject;
                if (IsWithin(source, GetKeepOpenElement(_popup)) || IsWithin(source, _popup.Child)) return;
                Close(); // Do not mark handled: the original outside control still receives its click.
            }

            private void OnClick(object sender, RoutedEventArgs e)
            {
                var button = e.OriginalSource as ButtonBase;
                if (button == null || !button.IsEnabled) return;
                // ToggleButton owns its two-way IsChecked update, including closing on a second click.
                if (ReferenceEquals(button, _popup.PlacementTarget)) return;
                Close(); // Also covers keyboard/automation activation and handled Click events.
            }

            private void Close()
            {
                var command = GetCloseCommand(_popup);
                if (_popup.IsOpen && command != null && command.CanExecute(null)) command.Execute(null);
            }

            private void OnOwnerClosed(object sender, EventArgs e) { Close(); Detach(); }

            private void Detach()
            {
                if (_window != null)
                {
                    _window.RemoveHandler(Mouse.PreviewMouseDownEvent, _mouseDown);
                    _window.RemoveHandler(ButtonBase.ClickEvent, _click);
                    _window.Closed -= OnOwnerClosed;
                    _window = null;
                }
                if (_child != null)
                {
                    _child.RemoveHandler(ButtonBase.ClickEvent, _click);
                    _child = null;
                }
            }

            public void Dispose()
            {
                Detach();
                _popup.Opened -= OnOpened;
                _popup.Closed -= OnClosed;
                _popup.Loaded -= OnLoaded;
                _popup.Unloaded -= OnUnloaded;
            }
        }
    }
}
