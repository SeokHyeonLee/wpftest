using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using RefDataMvvm.Core.Models;
using RefDataMvvm.Core.Mvvm;
using RefDataMvvm.Core.ViewModels;
using RefDataMvvm.Wpf;
using RefDataMvvm.Wpf.Behaviors;

namespace RefDataMvvm.Tests
{
    internal static partial class Program
    {
        private static void PopupViewModel()
        {
            var vm = new PopupMenuViewModel();
            int changes = 0;
            vm.PropertyChanged += (s, e) => { if (e.PropertyName == "IsOpen") changes++; };
            Equal(false, vm.IsOpen);
            vm.IsOpen = true;
            vm.IsOpen = true;
            Equal(1, changes);
            string before = vm.LastAction;
            vm.Items[1].Command.Execute(null);
            Equal(false, vm.Items[1].Command.CanExecute(null));
            Equal(before, vm.LastAction);
            Equal(true, vm.IsOpen);
            foreach (var item in vm.Items.Where(item => item.IsEnabled))
            {
                vm.IsOpen = true;
                item.Command.Execute(null);
                Equal(false, vm.IsOpen);
                Equal(item.Title + " 실행", vm.LastAction);
            }
            foreach (var command in new[] { vm.FirstCommand, vm.SecondCommand, vm.CloseCommand })
            {
                vm.IsOpen = true;
                command.Execute(null);
                Equal(false, vm.IsOpen);
            }
        }

        private static void PopupInteraction()
        {
            var errors = new BindingErrorListener();
            PresentationTraceSources.DataBindingSource.Listeners.Add(errors);
            using (var root = new MainViewModel(new CounterModel()))
            {
                // A real onscreen host is needed to verify Popup's screen-constrained placement over dim.
                var window = new MainWindow
                {
                    DataContext = root, ShowActivated = false, ShowInTaskbar = false,
                    WindowStartupLocation = WindowStartupLocation.Manual, Left = 80, Top = 80
                };
                try
                {
                    window.Show();
                    PumpFor(60);
                    var popup = (Popup)window.FindName("ActionPopup");
                    var toggle = (ToggleButton)window.FindName("PopupToggle");
                    var toolbar = (Grid)window.FindName("PopupToolbar");
                    var list = (Grid)window.FindName("PopupListGrid");
                    var dim = (Border)window.FindName("PopupDim");
                    var first = (Button)window.FindName("ToolbarFirst");
                    var second = (Button)window.FindName("ToolbarSecond");
                    Action<bool> assertOpen = expected =>
                    {
                        PumpFor(20);
                        Equal(expected, root.Menu.IsOpen);
                        Equal(expected, popup.IsOpen);
                        Equal((bool?)expected, toggle.IsChecked);
                        Equal(expected ? Visibility.Visible : Visibility.Collapsed, dim.Visibility);
                    };
                    Action open = () => { Toggle(toggle); assertOpen(true); };

                    open();
                    Equal(true, popup.StaysOpen);
                    Equal(true, dim.IsHitTestVisible);
                    Equal(true, ReferenceEquals(toolbar, PopupDismissBehavior.GetKeepOpenElement(popup)));
                    Equal(false, ReferenceEquals(PresentationSource.FromVisual(window), PresentationSource.FromVisual(popup.Child)));
                    var popupRect = new Rect(list.PointToScreen(new Point()), list.PointToScreen(new Point(list.ActualWidth, list.ActualHeight)));
                    var dimRect = new Rect(dim.PointToScreen(new Point()), dim.PointToScreen(new Point(dim.ActualWidth, dim.ActualHeight)));
                    Equal(true, popupRect.IntersectsWith(dimRect));

                    PressAt(toolbar, new Point(toolbar.ActualWidth - 4, 4));
                    assertOpen(true);
                    PressAt(list, new Point(4, 4));
                    assertOpen(true);
                    var items = Descendants<Button>(list).ToList();
                    Equal(3, items.Count);
                    Equal(false, items[1].IsEnabled);
                    PressAt(list, items[1].TranslatePoint(new Point(items[1].ActualWidth / 2, items[1].ActualHeight / 2), list));
                    assertOpen(true);
                    Equal("팝업을 열고 각 영역을 눌러 보세요.", root.Menu.LastAction);

                    // Even an already-handled descendant event reaches the outside-input adapter.
                    PressAt(dim, new Point(dim.ActualWidth - 5, dim.ActualHeight - 5), true);
                    assertOpen(false);
                    Equal(0, root.Left.ClickCount); // Dim consumed the hit; the counter underneath did not run.

                    open();
                    PressAt(window, new Point(5, 5));
                    assertOpen(false);

                    foreach (var button in new[] { first, second, items[0], items[2] })
                    {
                        open();
                        Invoke(button);
                        assertOpen(false);
                        Equal(button.Content + " 실행", root.Menu.LastAction);
                    }

                    // Test the behavior independently of the demo's self-closing action commands.
                    var savedCommand = first.Command;
                    int executions = 0;
                    first.SetCurrentValue(Button.CommandProperty, new RelayCommand(() => executions++));
                    open();
                    first.SetCurrentValue(UIElement.IsEnabledProperty, false);
                    PressAt(toolbar, first.TranslatePoint(new Point(first.ActualWidth / 2, first.ActualHeight / 2), toolbar));
                    assertOpen(true);
                    first.SetCurrentValue(UIElement.IsEnabledProperty, true);
                    RoutedEventHandler handledClick = (s, e) => e.Handled = true;
                    first.AddHandler(ButtonBase.ClickEvent, handledClick);
                    Invoke(first);
                    assertOpen(false);
                    Equal(1, executions);
                    first.RemoveHandler(ButtonBase.ClickEvent, handledClick);
                    first.SetCurrentValue(Button.CommandProperty, savedCommand);

                    // Verify the separate Popup presentation source with a command that does not close it.
                    var savedItemCommand = items[0].Command;
                    items[0].SetCurrentValue(Button.CommandProperty, new RelayCommand(() => executions++));
                    items[0].AddHandler(ButtonBase.ClickEvent, handledClick);
                    open();
                    Invoke(items[0]);
                    assertOpen(false);
                    Equal(2, executions);
                    items[0].RemoveHandler(ButtonBase.ClickEvent, handledClick);
                    items[0].SetCurrentValue(Button.CommandProperty, savedItemCommand);

                    for (int i = 0; i < 3; i++)
                    {
                        open();
                        Toggle(toggle);
                        assertOpen(false); // Must not close/reopen on the toggle's preview-down/Click sequence.
                    }
                    Equal(true, BindingOperations.IsDataBound(popup, Popup.IsOpenProperty));
                    Equal(true, BindingOperations.IsDataBound(toggle, ToggleButton.IsCheckedProperty));

                    open();
                    var content = window.Content;
                    window.Content = null;
                    assertOpen(false);
                    window.Content = content;
                    PumpFor(40);
                    open();
                    PressAt(dim, new Point(4, 4));
                    assertOpen(false);
                    open();
                    window.Close();
                    assertOpen(false);
                    Equal(0, errors.Messages.Count);
                }
                finally
                {
                    window.Close();
                    PresentationTraceSources.DataBindingSource.Listeners.Remove(errors);
                }
            }
        }

        private static void PressAt(UIElement root, Point point, bool handled = false)
        {
            var hit = root.InputHitTest(point) as UIElement;
            if (hit == null) throw new InvalidOperationException("No enabled input target at " + point);
            hit.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = Mouse.PreviewMouseDownEvent, Handled = handled
            });
        }

        private static void Invoke(Button button)
        {
            var peer = new ButtonAutomationPeer(button);
            ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
            PumpFor(20);
        }

        private static void Toggle(ToggleButton toggle)
        {
            PressAt(toggle, new Point(toggle.ActualWidth / 2, toggle.ActualHeight / 2));
            var peer = new ToggleButtonAutomationPeer(toggle);
            ((IToggleProvider)peer.GetPattern(PatternInterface.Toggle)).Toggle();
            toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, toggle));
        }
    }
}
