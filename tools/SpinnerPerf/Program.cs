using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using RefDataMvvm.Wpf.Controls;

internal static class Program
{
    private static readonly Func<long> Allocated = (Func<long>)Delegate.CreateDelegate(
        typeof(Func<long>), typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes));

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            if (args.Length != 3) throw new ArgumentException("Usage: SpinnerPerf <0|1|2|3> <0|1|64> <active|stopped>");
            int kind = int.Parse(args[0]), count = int.Parse(args[1]);
            bool active = args[2] == "active";
            if (kind < 0 || kind > 3 || (count != 0 && count != 1 && count != 64) ||
                (kind == 0) != (count == 0) || (args[2] != "active" && args[2] != "stopped"))
                throw new ArgumentException("Invalid scenario");
            Run(kind, count, active);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void Run(int kind, int count, bool active)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var grid = new UniformGrid { Rows = 8, Columns = 8, Margin = new Thickness(8) };
        var controls = new List<UserControl>();
        for (int i = 0; i < count; i++)
        {
            UserControl control = Create(kind);
            // Keep identical visible dots in the stopped baseline; only animation is disabled.
            control.Visibility = Visibility.Visible;
            SetActive(control, true);
            controls.Add(control);
            grid.Children.Add(control);
        }
        var window = new Window
        {
            Title = "Spinner benchmark (automatic): " + kind + " / " + count + " / " + (active ? "active" : "stopped"),
            Content = grid, Width = 420, Height = 450, Background = Brushes.White,
            ResizeMode = ResizeMode.NoResize, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        try
        {
            window.Show();
            window.UpdateLayout();
            Pump(2000, null);
            if (!active) foreach (var control in controls) SetActive(control, false);
            Pump(1000, null);
            Check(controls, active);
            Collect();
            long retainedBefore = GC.GetTotalMemory(false);
            var privateSamples = new List<long>(32);
            var workingSamples = new List<long>(32);
            using (var process = Process.GetCurrentProcess())
            {
                Action sample = () =>
                {
                    process.Refresh();
                    privateSamples.Add(process.PrivateMemorySize64);
                    workingSamples.Add(process.WorkingSet64);
                };
                sample();
                long allocBefore = Allocated();
                int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
                TimeSpan cpuBefore = process.TotalProcessorTime;
                var elapsed = Stopwatch.StartNew();
                Pump(6000, sample);
                double wallMs = elapsed.Elapsed.TotalMilliseconds;
                double cpuMs = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds;
                long allocated = Allocated() - allocBefore;
                gc0 = GC.CollectionCount(0) - gc0;
                gc1 = GC.CollectionCount(1) - gc1;
                gc2 = GC.CollectionCount(2) - gc2;
                sample();
                Check(controls, active);
                Collect();
                long retainedAfter = GC.GetTotalMemory(false);
                var source = PresentationSource.FromVisual(window);
                Console.WriteLine(string.Join(",", new object[]
                {
                    kind, count, active ? "active" : "stopped", wallMs.ToString("F2"), cpuMs.ToString("F2"),
                    (cpuMs / wallMs * 100).ToString("F3"),
                    (cpuMs / wallMs * 100 / Environment.ProcessorCount).ToString("F3"),
                    (Median(privateSamples) / 1048576.0).ToString("F3"),
                    (privateSamples.Max() / 1048576.0).ToString("F3"),
                    (Median(workingSamples) / 1048576.0).ToString("F3"),
                    (workingSamples.Max() / 1048576.0).ToString("F3"),
                    (retainedBefore / 1048576.0).ToString("F3"), (retainedAfter / 1048576.0).ToString("F3"),
                    (allocated / 1024.0 / (wallMs / 1000)).ToString("F3"), gc0, gc1, gc2,
                    RenderCapability.Tier >> 16, Environment.ProcessorCount, Environment.Is64BitProcess,
                    (source.CompositionTarget.TransformToDevice.M11 * 96).ToString("F0"), Environment.Version
                }));
            }
        }
        finally { window.Close(); app.Shutdown(); }
    }

    private static UserControl Create(int kind)
    {
        switch (kind)
        {
            case 1: return new LoadingSpinner();
            case 2: return new LoadingSpinner2();
            case 3: return new LoadingSpinner3();
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private static void SetActive(UserControl control, bool active)
    {
        if (control is LoadingSpinner a) a.IsActivate = active;
        else if (control is LoadingSpinner2 b) b.IsActivate = active;
        else ((LoadingSpinner3)control).IsActivate = active;
    }

    private static void Check(IEnumerable<UserControl> controls, bool active)
    {
        foreach (var control in controls)
        {
            if (!control.IsLoaded || !control.IsVisible || control.ActualWidth != 32 || control.ActualHeight != 32)
                throw new InvalidOperationException("Control is not visibly laid out at 32 DIP");
            var dots = control.FindName("Dots") as Canvas;
            bool running = dots != null
                ? dots.Children.Cast<UIElement>().All(dot => dot.HasAnimatedProperties)
                : ((RotateTransform)((Canvas)control.FindName("Rotor")).RenderTransform).HasAnimatedProperties;
            if (running != active) throw new InvalidOperationException("Unexpected animation clock state");
        }
    }

    private static double Median(List<long> values)
    {
        var sorted = values.OrderBy(value => value).ToArray();
        return (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2.0;
    }

    private static void Collect()
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    }

    private static void Pump(int milliseconds, Action sample)
    {
        var frame = new DispatcherFrame();
        var watch = Stopwatch.StartNew();
        var timer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(250) };
        EventHandler tick = (s, e) =>
        {
            sample?.Invoke();
            if (watch.ElapsedMilliseconds >= milliseconds) frame.Continue = false;
        };
        timer.Tick += tick;
        timer.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); timer.Tick -= tick; }
    }
}
