using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ellipse = System.Windows.Shapes.Ellipse;
using System.Windows.Threading;
using RefDataMvvm.Core.Data;
using RefDataMvvm.Core.Models;
using RefDataMvvm.Core.Mvvm;
using RefDataMvvm.Core.ViewModels;
using RefDataMvvm.Wpf;
using RefDataMvvm.Wpf.Controls;

namespace RefDataMvvm.Tests
{
    internal static class Program
    {
        private static int _failed;
        private static int _passed;

        [STAThread]
        private static int Main(string[] args)
        {
            Run("References share original data; writes do not notify", SharedReferences);
            Run("changed() and changedflag notify all observers", NotificationTriggers);
            Run("Model publishes a complete click state", CompleteModelState);
            Run("Two VMs follow commands and external Model changes", TwoViewModels);
            Run("External RefData writes also refresh the VM", ExternalReferenceWrite);
            Run("Reset and command availability synchronize", ResetAndCommands);
            Run("Recreating a VM preserves Model data", RecreateViewModel);
            Run("Disposed VMs unsubscribe and reject commands", DisposeUnsubscribes);
            Run("Counter overflow leaves Model unchanged", OverflowProtection);
            Run("Click toggles shared RefData bool; reset clears it", SpinnerToggle);
            Run("External bool changes notify; replacement VMs retain state", ExternalSpinnerChanges);
            Run("Real WPF XAML bindings and commands", () => WpfBindings(args.Length > 0 ? args[0] : null));
            Console.WriteLine("\nPassed: " + _passed + ", Failed: " + _failed);
            return _failed == 0 ? 0 : 1;
        }

        private static void Run(string name, Action test)
        {
            try { test(); _passed++; Console.WriteLine("PASS " + name); }
            catch (Exception ex) { _failed++; Console.WriteLine("FAIL " + name + "\n" + ex); }
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException("Expected: " + expected + "; actual: " + actual);
        }

        private static void SharedReferences()
        {
            var data = new ObservableData<int>(0);
            RefData<int> a = data.GetReference();
            RefData<int> b = data.GetReference();
            int notifications = 0;
            b.Changed += (s, e) => notifications++;
            a.value = 1;
            Equal(1, b.value);
            Equal(0, notifications);
            a.changed();
            Equal(1, notifications);
        }

        private static void NotificationTriggers()
        {
            var data = new ObservableData<int>(0);
            var a = data.GetReference();
            var b = data.GetReference();
            int first = 0, second = 0;
            a.Changed += (s, e) => first++;
            b.Changed += (s, e) => second++;
            a.value = 10;
            a.changedflag = false;
            Equal(0, first);
            a.changedflag = true;
            b.changed();
            Equal(2, first);
            Equal(2, second);
            Equal(10, b.value);
        }

        private static void CompleteModelState()
        {
            var now = new DateTime(2026, 9, 5, 12, 30, 10);
            var model = new CounterModel(() => now);
            int notifications = 0;
            model.GetClickCountReference().Changed += (s, e) =>
            {
                Equal(1, model.GetClickCountReference().value);
                Equal("test 버튼을 눌렀습니다. (총 1회)", model.GetInformationReference().value);
                Equal<DateTime?>(now, model.GetLastClickedAtReference().value);
                Equal(true, model.GetIsActivateReference().value);
                notifications++;
            };
            model.RecordClick("test");
            Equal(1, notifications);
        }

        private static void TwoViewModels()
        {
            var model = new CounterModel(() => new DateTime(2026, 9, 5, 12, 30, 10));
            using (var left = new CounterViewModel(model, "left"))
            using (var right = new CounterViewModel(model, "right"))
            {
                var changed = new List<string>();
                right.PropertyChanged += (s, e) => changed.Add(e.PropertyName);
                left.ClickCommand.Execute(null);
                Equal(1, left.ClickCount);
                Equal(1, right.ClickCount);
                Equal("left 버튼을 눌렀습니다. (총 1회)", right.Information);
                Equal("2026-09-05 12:30:10", right.LastClickedAtText);
                Equal("ClickCount,Information,LastClickedAtText,IsActivate", string.Join(",", changed));
                model.RecordClick("external model");
                Equal(2, left.ClickCount);
                Equal(2, right.ClickCount);
            }
        }

        private static void ExternalReferenceWrite()
        {
            var model = new CounterModel();
            using (var vm = new CounterViewModel(model, "panel"))
            {
                int notifications = 0;
                vm.PropertyChanged += (s, e) => { if (e.PropertyName == "ClickCount") notifications++; };
                RefData<int> a = model.GetClickCountReference();
                a.value = 42;
                Equal(42, vm.ClickCount); // getter always reads the original.
                Equal(0, notifications);
                a.changedflag = true;
                Equal(1, notifications);
            }
        }

        private static void ResetAndCommands()
        {
            var model = new CounterModel();
            using (var left = new CounterViewModel(model, "left"))
            using (var right = new CounterViewModel(model, "right"))
            {
                Equal(false, right.ResetCommand.CanExecute(null));
                int commandNotifications = 0;
                right.ResetCommand.CanExecuteChanged += (s, e) => commandNotifications++;
                left.ClickCommand.Execute(null);
                Equal(true, right.ResetCommand.CanExecute(null));
                right.ResetCommand.Execute(null);
                Equal(0, left.ClickCount);
                Equal("기록 없음", left.LastClickedAtText);
                Equal("초기화했습니다. 버튼을 눌러 다시 시작하세요.", right.Information);
                Equal(false, left.ResetCommand.CanExecute(null));
                Equal(4, commandNotifications);
            }
        }

        private static void RecreateViewModel()
        {
            var model = new CounterModel();
            using (var root = new MainViewModel(model))
            {
                root.Left.ClickCommand.Execute(null);
                var oldRight = root.Right;
                int oldNotifications = 0, rootNotifications = 0;
                oldRight.PropertyChanged += (s, e) => oldNotifications++;
                root.PropertyChanged += (s, e) => { if (e.PropertyName == "Right") rootNotifications++; };
                root.RecreateRightCommand.Execute(null);
                Equal(false, ReferenceEquals(oldRight, root.Right));
                Equal(1, root.Right.ClickCount);
                root.Right.ClickCommand.Execute(null);
                Equal(2, root.Left.ClickCount);
                Equal(0, oldNotifications);
                Equal(1, rootNotifications);
                Equal(false, oldRight.ClickCommand.CanExecute(null));
            }
        }

        private static void DisposeUnsubscribes()
        {
            var model = new CounterModel();
            var vm = new CounterViewModel(model, "panel");
            int notifications = 0;
            vm.PropertyChanged += (s, e) => notifications++;
            vm.Dispose();
            vm.Dispose();
            model.RecordClick("external");
            vm.ClickCommand.Execute(null);
            vm.ResetCommand.Execute(null);
            Equal(0, notifications);
            Equal(1, model.GetClickCountReference().value);
            Equal(false, vm.ClickCommand.CanExecute(null));
            Equal(false, vm.ResetCommand.CanExecute(null));
        }

        private static void OverflowProtection()
        {
            var model = new CounterModel();
            using (var vm = new CounterViewModel(model, "panel"))
            {
                var count = model.GetClickCountReference();
                count.value = int.MaxValue;
                count.changed();
                Equal(false, vm.ClickCommand.CanExecute(null));
                bool threw = false;
                try { model.RecordClick("external"); }
                catch (OverflowException) { threw = true; }
                Equal(true, threw);
                Equal(int.MaxValue, count.value);
                Equal<DateTime?>(null, model.GetLastClickedAtReference().value);
                Equal("아직 버튼을 누르지 않았습니다.", model.GetInformationReference().value);
                Equal(false, model.GetIsActivateReference().value);
            }
        }

        private static void SpinnerToggle()
        {
            var model = new CounterModel();
            using (var root = new MainViewModel(model))
            {
                Equal(false, root.Left.IsActivate);
                root.Left.ClickCommand.Execute(null);
                Equal(true, root.Left.IsActivate);
                Equal(true, root.Right.IsActivate);
                root.Right.ClickCommand.Execute(null);
                Equal(false, root.Left.IsActivate);
                Equal(false, root.Right.IsActivate);
                root.Left.ClickCommand.Execute(null);
                root.Right.ResetCommand.Execute(null);
                Equal(false, root.Left.IsActivate);
                Equal(false, root.Right.IsActivate);
                Equal(0, root.Left.ClickCount);
            }
        }

        private static void ExternalSpinnerChanges()
        {
            var model = new CounterModel();
            using (var root = new MainViewModel(model))
            {
                int notifications = 0;
                root.Left.PropertyChanged += (s, e) => { if (e.PropertyName == "IsActivate") notifications++; };
                RefData<bool> active = model.GetIsActivateReference();
                active.value = true;
                Equal(0, notifications);
                active.changedflag = true;
                Equal(1, notifications);
                Equal(true, root.Left.ResetCommand.CanExecute(null));
                var oldRight = root.Right;
                int oldNotifications = 0;
                oldRight.PropertyChanged += (s, e) => oldNotifications++;
                root.RecreateRightCommand.Execute(null);
                Equal(true, root.Right.IsActivate);
                root.Left.ResetCommand.Execute(null);
                Equal(false, root.Right.IsActivate);
                Equal(0, oldNotifications);
            }
        }

        private static void WpfBindings(string renderPath)
        {
            var app = new App();
            app.InitializeComponent();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var errors = new BindingErrorListener();
            PresentationTraceSources.DataBindingSource.Listeners.Add(errors);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            var model = new CounterModel(() => new DateTime(2026, 9, 5, 12, 30, 10));
            using (var root = new MainViewModel(model))
            {
                // 실제 Loaded/IsVisible/animation clock을 검증하는 화면 밖의 테스트 창.
                var window = new MainWindow
                {
                    DataContext = root, ShowActivated = false, ShowInTaskbar = false,
                    WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000
                };
                try
                {
                    window.Show();
                    var content = (FrameworkElement)window.Content;
                    Layout(content);
                    AssertDisplayedCounts(content, "0");
                    AssertSpinners(content, false);
                    var buttons = Descendants<Button>(content).ToList();
                    Equal(5, buttons.Count);
                    var click = buttons.First(b => Equals(b.Content, "눌러서 +1"));
                    Equal(true, click.Command != null);
                    double originalButtonWidth = click.ActualWidth;
                    click.Command.Execute(click.CommandParameter);
                    Layout(content);
                    AssertDisplayedCounts(content, "1");
                    AssertSpinners(content, true);
                    Equal(originalButtonWidth, click.ActualWidth);

                    var recreate = buttons.Single(b => Equals(b.Content, "오른쪽 VM 다시 만들기"));
                    recreate.Command.Execute(recreate.CommandParameter);
                    Layout(content);
                    AssertDisplayedCounts(content, "1");
                    AssertSpinners(content, true);
                    model.RecordClick("외부 Model");
                    Layout(content);
                    AssertDisplayedCounts(content, "2");
                    AssertSpinners(content, false);

                    var rightClick = Descendants<Button>(content).Last(b => Equals(b.Content, "눌러서 +1"));
                    rightClick.Command.Execute(rightClick.CommandParameter);
                    Layout(content);
                    AssertDisplayedCounts(content, "3");
                    AssertSpinners(content, true);
                    Equal(true, Descendants<TextBlock>(content).Any(t => t.Text == "오른쪽 패널 버튼을 눌렀습니다. (총 3회)"));

                    if (renderPath != null)
                        Render(content, renderPath);
                    VerifySpinnerLifecycleAndBrush(content);
                    VerifyOpacitySpinnerLifecycleAndBrush(content);
                    VerifyDiscreteSpinnerLifecycleAndBrush(content);
                    VerifyWindowAndViewportSuspension(window, content);
                    Equal(0, errors.Messages.Count);
                }
                finally
                {
                    window.Close();
                    PresentationTraceSources.DataBindingSource.Listeners.Remove(errors);
                }
            }
        }

        private static void AssertSpinners(DependencyObject content, bool active)
        {
            var spinners = Descendants<LoadingSpinner>(content).ToList();
            Equal(1, spinners.Count);
            foreach (var spinner in spinners)
            {
                Equal(active, spinner.IsActivate);
                Equal(active ? Visibility.Visible : Visibility.Collapsed, spinner.Visibility);
                var rotor = (Canvas)spinner.FindName("Rotor");
                var rotation = (RotateTransform)rotor.RenderTransform;
                Equal(active, rotation.HasAnimatedProperties);
                Equal(false, spinner.IsHitTestVisible);
                if (!active) continue;

                var dots = rotor.Children.OfType<Ellipse>().ToList();
                Equal(8, dots.Count);
                double opacityStep = -1;
                for (int i = 0; i < dots.Count; i++)
                {
                    var dot = dots[i];
                    double x = Canvas.GetLeft(dot) + dot.Width / 2;
                    double y = Canvas.GetTop(dot) + dot.Height / 2;
                    Near(22, Math.Sqrt((x - 32) * (x - 32) + (y - 32) * (y - 32)));
                    var opposite = dots[(i + 4) % 8];
                    Near(64, x + Canvas.GetLeft(opposite) + opposite.Width / 2);
                    Near(64, y + Canvas.GetTop(opposite) + opposite.Height / 2);
                    Point rotated = rotation.Transform(new Point(x, y));
                    Near(22, (rotated - new Point(32, 32)).Length);
                    Equal(true, ReferenceEquals(spinner.DotBrush, dot.Fill));
                    if (i > 0)
                    {
                        double nextStep = dot.Opacity - dots[i - 1].Opacity;
                        Equal(true, nextStep > opacityStep); // 의도한 비선형 단계
                        opacityStep = nextStep;
                    }
                }
                Near(0.12, dots[0].Opacity);
                Near(1.0, dots[7].Opacity);

                var button = FindButtonHost(spinner).Children.OfType<Button>().Single();
                Point position = spinner.TranslatePoint(new Point(0, 0), button);
                Near(button.ActualWidth + 10, position.X + spinner.ActualWidth);
                Near(-16, position.Y);
            }
            if (active)
            {
                Equal("#FF2463CE", spinners[0].DotBrush.ToString());
            }

            var fixedSpinners = Descendants<LoadingSpinner2>(content).ToList();
            Equal(1, fixedSpinners.Count);
            var fixedSpinner = fixedSpinners.Single();
            Equal(active, fixedSpinner.IsActivate);
            Equal(active ? Visibility.Visible : Visibility.Collapsed, fixedSpinner.Visibility);
            Equal(false, fixedSpinner.IsHitTestVisible);
            Equal("#FF7C3AED", fixedSpinner.DotBrush.ToString());
            var canvas = (Canvas)fixedSpinner.FindName("Dots");
            Equal(true, canvas.RenderTransform.Value.IsIdentity);
            Equal(true, canvas.LayoutTransform.Value.IsIdentity);
            var fixedDots = canvas.Children.OfType<Ellipse>().ToArray();
            Equal(8, fixedDots.Length);
            foreach (var dot in fixedDots)
            {
                Equal(active, dot.HasAnimatedProperties);
                Equal(true, ReferenceEquals(fixedSpinner.DotBrush, dot.Fill));
                Point center = new Point(Canvas.GetLeft(dot) + 4, Canvas.GetTop(dot) + 4);
                Near(22, (center - new Point(32, 32)).Length);
                Equal(true, dot.Opacity >= 0.12 && dot.Opacity <= 1);
            }
            if (active)
            {
                var button = FindButtonHost(fixedSpinner).Children.OfType<Button>().Single();
                Point position = fixedSpinner.TranslatePoint(new Point(), button);
                Near(button.ActualWidth + 10, position.X + fixedSpinner.ActualWidth);
                Near(-16, position.Y);
            }
        }

        private static Grid FindButtonHost(DependencyObject child)
        {
            for (var parent = VisualTreeHelper.GetParent(child); parent != null; parent = VisualTreeHelper.GetParent(parent))
            {
                var grid = parent as Grid;
                if (grid != null && grid.Children.OfType<Button>().Any()) return grid;
            }
            throw new InvalidOperationException("Spinner button host not found.");
        }

        private static void VerifySpinnerLifecycleAndBrush(FrameworkElement content)
        {
            var existing = Descendants<LoadingSpinner>(content).First();
            var host = FindButtonHost(existing);
            var input = new SpinnerInput { Active = true, Brush = Brushes.Crimson };
            var probe = new LoadingSpinner { Width = 32, Height = 32 };
            probe.SetBinding(LoadingSpinner.IsActivateProperty, new Binding("Active") { Source = input });
            probe.SetBinding(LoadingSpinner.DotBrushProperty, new Binding("Brush") { Source = input });
            host.Children.Add(probe);
            try
            {
                Layout(content);
                var rotor = (Canvas)probe.FindName("Rotor");
                var rotation = (RotateTransform)rotor.RenderTransform;
                Equal(true, rotation.HasAnimatedProperties); // active set before Loaded
                double firstAngle = rotation.Angle;
                PumpFor(160);
                Equal(true, Math.Abs(rotation.Angle - firstAngle) > 1);
                Near(32, rotation.Transform(new Point(32, 32)).X);
                Near(32, rotation.Transform(new Point(32, 32)).Y);

                input.Brush = Brushes.SeaGreen;
                Layout(content);
                Equal(true, ReferenceEquals(Brushes.SeaGreen, probe.DotBrush));
                foreach (var dot in rotor.Children.OfType<Ellipse>())
                    Equal(true, ReferenceEquals(Brushes.SeaGreen, dot.Fill));

                input.Active = false;
                Layout(content);
                Equal(Visibility.Collapsed, probe.Visibility);
                Equal(false, rotation.HasAnimatedProperties);
                Near(0, rotation.Angle);
                input.Active = true;
                Layout(content);
                Equal(true, rotation.HasAnimatedProperties);

                host.Visibility = Visibility.Hidden;
                Layout(content);
                Equal(false, rotation.HasAnimatedProperties);
                host.Visibility = Visibility.Visible;
                Layout(content);
                Equal(true, rotation.HasAnimatedProperties);

                host.Children.Remove(probe);
                Layout(content);
                Equal(false, rotation.HasAnimatedProperties);
                host.Children.Add(probe);
                Layout(content);
                Equal(true, rotation.HasAnimatedProperties);

                // 부모의 반올림 설정과 다양한 표시 크기에서도 좌표계는 그대로다.
                foreach (double size in new[] { 24.0, 32.0, 40.0, 48.0 })
                {
                    probe.Width = size;
                    probe.Height = size;
                    Layout(content);
                    Near(64, rotor.ActualWidth);
                    Near(64, rotor.ActualHeight);
                    Near(32, rotation.CenterX);
                    Near(32, rotation.CenterY);
                    Equal(false, rotor.UseLayoutRounding);
                    Equal(false, rotor.SnapsToDevicePixels);
                }
            }
            finally
            {
                host.Visibility = Visibility.Visible;
                host.Children.Remove(probe);
                Layout(content);
            }
        }

        private static void VerifyOpacitySpinnerLifecycleAndBrush(FrameworkElement content)
        {
            var host = FindButtonHost(Descendants<LoadingSpinner2>(content).Single());
            var input = new SpinnerInput { Active = true, Brush = Brushes.Crimson };
            var probe = new LoadingSpinner2();
            probe.SetBinding(LoadingSpinner2.IsActivateProperty, new Binding("Active") { Source = input });
            probe.SetBinding(LoadingSpinner2.DotBrushProperty, new Binding("Brush") { Source = input });
            host.Children.Add(probe);
            try
            {
                Layout(content);
                var canvas = (Canvas)probe.FindName("Dots");
                var dots = canvas.Children.OfType<Ellipse>().ToArray();
                Equal(true, dots.All(d => d.HasAnimatedProperties));
                var positions = dots.Select(d => d.TranslatePoint(new Point(4, 4), canvas)).ToArray();
                var opacities = dots.Select(d => d.Opacity).ToArray();
                for (int frame = 0; frame < 3; frame++)
                {
                    PumpFor(140);
                    Equal(true, canvas.RenderTransform.Value.IsIdentity);
                    for (int i = 0; i < dots.Length; i++)
                    {
                        Point current = dots[i].TranslatePoint(new Point(4, 4), canvas);
                        Near(positions[i].X, current.X);
                        Near(positions[i].Y, current.Y);
                        Equal(true, dots[i].RenderTransform.Value.IsIdentity);
                        Near(8, dots[i].ActualWidth);
                        Near(8, dots[i].ActualHeight);
                    }
                }
                Equal(true, dots.Where((dot, i) => Math.Abs(dot.Opacity - opacities[i]) > 0.02).Count() >= 4);

                input.Brush = Brushes.SeaGreen;
                Layout(content);
                Equal(true, ReferenceEquals(Brushes.SeaGreen, probe.DotBrush));
                Equal(true, dots.All(d => ReferenceEquals(Brushes.SeaGreen, d.Fill)));

                // 비선형 초기 밝기와 반복 중단/재시작 후 clock이 남지 않는지 확인한다.
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    input.Active = false;
                    Layout(content);
                    Equal(Visibility.Collapsed, probe.Visibility);
                    Equal(true, dots.All(d => !d.HasAnimatedProperties));
                    var baseline = dots.Select(d => d.Opacity).OrderBy(v => v).ToArray();
                    Near(0.12, baseline.First());
                    Near(1, baseline.Last());
                    Equal(true, Math.Abs((baseline[1] - baseline[0]) - (baseline[4] - baseline[3])) > 0.01);
                    input.Active = true;
                    Layout(content);
                    Equal(true, dots.All(d => d.HasAnimatedProperties));
                }

                host.Visibility = Visibility.Hidden;
                Layout(content);
                Equal(true, dots.All(d => !d.HasAnimatedProperties));
                host.Visibility = Visibility.Visible;
                Layout(content);
                Equal(true, dots.All(d => d.HasAnimatedProperties));
                host.Children.Remove(probe);
                Layout(content);
                Equal(true, dots.All(d => !d.HasAnimatedProperties));
                host.Children.Add(probe);
                Layout(content);
                Equal(true, dots.All(d => d.HasAnimatedProperties));
            }
            finally
            {
                host.Visibility = Visibility.Visible;
                host.Children.Remove(probe);
                Layout(content);
            }
        }

        private static void VerifyDiscreteSpinnerLifecycleAndBrush(FrameworkElement content)
        {
            var host = FindButtonHost(Descendants<LoadingSpinner>(content).Single());
            var input = new SpinnerInput { Active = true, Brush = Brushes.Crimson };
            var probe = new LoadingSpinner3();
            probe.SetBinding(LoadingSpinner3.IsActivateProperty, new Binding("Active") { Source = input });
            probe.SetBinding(LoadingSpinner3.DotBrushProperty, new Binding("Brush") { Source = input });
            host.Children.Add(probe);
            try
            {
                Layout(content);
                var rotor = (Canvas)probe.FindName("Rotor");
                var rotation = (RotateTransform)rotor.RenderTransform;
                var dots = rotor.Children.OfType<Ellipse>().ToArray();
                Equal(8, dots.Length);
                Equal(true, rotation.HasAnimatedProperties);
                Near(32, rotation.CenterX);
                Near(32, rotation.CenterY);
                var opacities = dots.Select(d => d.Opacity).ToArray();

                // Sample twice inside every 125 ms hold, including the one-second repeat boundary.
                var elapsed = Stopwatch.StartNew();
                for (int step = 0; step <= 8; step++)
                {
                    foreach (int offset in new[] { 45, 85 })
                    {
                        int remaining = step * 125 + offset - (int)elapsed.ElapsedMilliseconds;
                        if (remaining > 0) PumpFor(remaining);
                        Near((step % 8) * 45, rotation.Angle);
                        for (int i = 0; i < dots.Length; i++) Near(opacities[i], dots[i].Opacity);
                    }
                }
                input.Brush = Brushes.SeaGreen;
                Layout(content);
                Equal(true, dots.All(d => ReferenceEquals(Brushes.SeaGreen, d.Fill)));
                input.Active = false;
                Layout(content);
                Equal(Visibility.Collapsed, probe.Visibility);
                Equal(false, rotation.HasAnimatedProperties);
                Near(0, rotation.Angle);
                input.Active = true;
                Layout(content);
                Equal(true, rotation.HasAnimatedProperties);
                Near(0, rotation.Angle);
                host.Visibility = Visibility.Hidden;
                Layout(content);
                Equal(false, rotation.HasAnimatedProperties);
                host.Visibility = Visibility.Visible;
                Layout(content);
                Equal(true, rotation.HasAnimatedProperties);
                host.Children.Remove(probe);
                Layout(content);
                Equal(false, rotation.HasAnimatedProperties);
                host.Children.Add(probe);
                Layout(content);
                Equal(true, rotation.HasAnimatedProperties);
            }
            finally
            {
                host.Visibility = Visibility.Visible;
                host.Children.Remove(probe);
                Layout(content);
            }
        }

        private static void VerifyWindowAndViewportSuspension(Window window, FrameworkElement content)
        {
            var rotating = Descendants<LoadingSpinner>(content).Single();
            var fading = Descendants<LoadingSpinner2>(content).Single();
            AssertAnimationClocks(rotating, fading, true);

            window.WindowState = WindowState.Minimized;
            PumpFor(80);
            AssertAnimationClocks(rotating, fading, false);
            Equal(true, rotating.IsActivate); // Model bool은 유지하고 표시 작업만 중단한다.
            Equal(true, fading.IsActivate);
            window.WindowState = WindowState.Normal;
            Layout(content);
            AssertAnimationClocks(rotating, fading, true);

            window.Hide();
            PumpFor(40);
            AssertAnimationClocks(rotating, fading, false);
            window.Show();
            Layout(content);
            AssertAnimationClocks(rotating, fading, true);

            var scrollRotating = new LoadingSpinner { IsActivate = true, Margin = new Thickness(8) };
            var scrollFading = new LoadingSpinner2 { IsActivate = true, Margin = new Thickness(8) };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(scrollRotating);
            row.Children.Add(scrollFading);
            var stack = new StackPanel();
            stack.Children.Add(new Border { Height = 1400 });
            stack.Children.Add(row);
            var scroll = new ScrollViewer
            {
                Content = stack, CanContentScroll = false,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            window.Content = scroll;
            try
            {
                window.UpdateLayout();
                PumpFor(60);
                AssertAnimationClocks(rotating, fading, false); // 이전 화면 Unloaded
                Equal(true, scrollRotating.IsVisible); // IsVisible만으로는 감지 못 하는 경우
                AssertAnimationClocks(scrollRotating, scrollFading, false);
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    scroll.ScrollToEnd();
                    window.UpdateLayout();
                    PumpFor(40);
                    AssertAnimationClocks(scrollRotating, scrollFading, true);
                    scroll.ScrollToTop();
                    window.UpdateLayout();
                    PumpFor(40);
                    AssertAnimationClocks(scrollRotating, scrollFading, false);
                }
            }
            finally
            {
                window.Content = content;
                Layout(content);
            }
            AssertAnimationClocks(scrollRotating, scrollFading, false);
            AssertAnimationClocks(rotating, fading, true);
        }

        private static void AssertAnimationClocks(LoadingSpinner rotating, LoadingSpinner2 fading, bool running)
        {
            var rotor = (Canvas)rotating.FindName("Rotor");
            Equal(running, ((RotateTransform)rotor.RenderTransform).HasAnimatedProperties);
            var dots = (Canvas)fading.FindName("Dots");
            foreach (var dot in dots.Children.OfType<Ellipse>())
                Equal(running, dot.HasAnimatedProperties);
        }

        private static void Near(double expected, double actual)
        {
            if (Math.Abs(expected - actual) > 0.00001)
                throw new InvalidOperationException("Expected approximately: " + expected + "; actual: " + actual);
        }

        private static void PumpFor(int milliseconds)
        {
            var frame = new DispatcherFrame();
            // Keep the bounded wait from being starved by rendering during window transitions.
            var timer = new DispatcherTimer(DispatcherPriority.Send)
                { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            EventHandler tick = (s, e) => { timer.Stop(); frame.Continue = false; };
            timer.Tick += tick;
            timer.Start();
            try { Dispatcher.PushFrame(frame); }
            finally { timer.Stop(); timer.Tick -= tick; }
        }

        private sealed class SpinnerInput : ObservableObject
        {
            private bool _active;
            private Brush _brush;
            public bool Active
            {
                get { return _active; }
                set { _active = value; OnPropertyChanged(); }
            }
            public Brush Brush
            {
                get { return _brush; }
                set { _brush = value; OnPropertyChanged(); }
            }
        }

        private static void Layout(FrameworkElement content)
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
            content.Measure(new Size(1000, 700));
            content.Arrange(new Rect(0, 0, 1000, 700));
            content.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
        }

        private static void AssertDisplayedCounts(DependencyObject content, string expected)
        {
            var counts = Descendants<TextBlock>(content)
                .Where(t => BindingOperations.GetBinding(t, TextBlock.TextProperty)?.Path.Path == "ClickCount").ToList();
            Equal(2, counts.Count);
            foreach (var count in counts) Equal(expected, count.Text);
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match) yield return match;
                foreach (var descendant in Descendants<T>(child)) yield return descendant;
            }
        }

        private static void Render(FrameworkElement content, string path)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var rect = new Rect(0, 0, 1000, 700);
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(243, 246, 250)), null, rect);
            }
            var bitmap = new RenderTargetBitmap(1000, 700, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Render(content);
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(bitmap));
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            using (var stream = File.Create(fullPath)) png.Save(stream);
            Console.WriteLine("Rendered: " + fullPath);
        }

        private sealed class BindingErrorListener : TraceListener
        {
            public List<string> Messages { get; } = new List<string>();
            public override void Write(string message) { if (!string.IsNullOrEmpty(message)) Messages.Add(message); }
            public override void WriteLine(string message) { Write(message); }
        }
    }
}
