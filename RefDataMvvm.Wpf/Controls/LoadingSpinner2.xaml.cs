using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace RefDataMvvm.Wpf.Controls
{
    /// <summary>
    /// 점의 위치/크기는 고정하고 Opacity의 위상만 순환시키는 스피너.
    /// Model이나 DataContext를 소유하지 않는 표시 전용 UserControl이다.
    /// </summary>
    public partial class LoadingSpinner2 : UserControl
    {
        public static readonly DependencyProperty IsActivateProperty = DependencyProperty.Register(
            nameof(IsActivate), typeof(bool), typeof(LoadingSpinner2),
            new FrameworkPropertyMetadata(false, OnIsActivateChanged));

        public static readonly DependencyProperty DotBrushProperty = DependencyProperty.Register(
            nameof(DotBrush), typeof(Brush), typeof(LoadingSpinner2),
            new FrameworkPropertyMetadata(Brushes.DimGray));

        private const int DotCount = 8;
        private const int SamplesPerCycle = 64;
        private const double OpacityGamma = 2.2;
        // 8×65개 키프레임은 프로세스에서 한 번 계산/Freeze하여 공유한다.
        // 각 인스턴스는 실행 중일 때만 독립적인 clock을 가진다.
        private static readonly ParallelTimeline Pulse = CreatePulse();
        private readonly SpinnerActivityMonitor _activity;
        private ClockGroup _clock;

        public LoadingSpinner2()
        {
            InitializeComponent();
            CreateDots();
            _activity = new SpinnerActivityMonitor(this, () => IsActivate, UpdateAnimation);
        }

        public bool IsActivate
        {
            get { return (bool)GetValue(IsActivateProperty); }
            set { SetValue(IsActivateProperty, value); }
        }

        public Brush DotBrush
        {
            get { return (Brush)GetValue(DotBrushProperty); }
            set { SetValue(DotBrushProperty, value); }
        }

        private void CreateDots()
        {
            for (int i = 0; i < DotCount; i++)
            {
                double angle = -Math.PI / 2 + i * 2 * Math.PI / DotCount;
                var dot = new Ellipse
                {
                    Width = 8, Height = 8,
                    Opacity = OpacityAt(0, i),
                    UseLayoutRounding = false, SnapsToDevicePixels = false
                };
                dot.SetBinding(Shape.FillProperty, new Binding(nameof(DotBrush)) { Source = this });
                Canvas.SetLeft(dot, 32 + 22 * Math.Cos(angle) - 4);
                Canvas.SetTop(dot, 32 + 22 * Math.Sin(angle) - 4);
                Dots.Children.Add(dot);
            }
        }

        private static ParallelTimeline CreatePulse()
        {
            var pulse = new ParallelTimeline
            {
                Duration = new Duration(TimeSpan.FromSeconds(1)),
                RepeatBehavior = RepeatBehavior.Forever
            };
            for (int i = 0; i < DotCount; i++)
            {
                var animation = new DoubleAnimationUsingKeyFrames
                {
                    Duration = pulse.Duration
                };
                // 각 점은 1/8주기씩 위상이 다르다. 하나의 clock 그룹으로 동시에 시작한다.
                // 곡선을 샘플링한 키프레임 사이도 보간하므로 밝기가 단계적으로 튀지 않는다.
                for (int sample = 0; sample <= SamplesPerCycle; sample++)
                {
                    double time = (double)sample / SamplesPerCycle;
                    animation.KeyFrames.Add(new LinearDoubleKeyFrame(
                        OpacityAt(time, i), KeyTime.FromTimeSpan(TimeSpan.FromSeconds(time))));
                }
                pulse.Children.Add(animation);
            }
            pulse.Freeze();
            return pulse;
        }

        private static double OpacityAt(double time, int dotIndex)
        {
            double phase = time - (double)dotIndex / DotCount;
            phase -= Math.Floor(phase);

            // 밝은 머리 뒤로 긴 꼬리가 따라간다: 7/8주기 동안 어두워지고,
            // 마지막 1/8주기에 다시 밝아진다. 주기 양 끝의 값은 정확히 같다.
            const double fadePortion = 7.0 / 8.0;
            double envelope = phase <= fadePortion
                ? 1 - phase / fadePortion
                : (phase - fadePortion) / (1 - fadePortion);
            double smooth = envelope * envelope * (3 - 2 * envelope);
            return 0.12 + 0.88 * Math.Pow(smooth, OpacityGamma);
        }

        private static void OnIsActivateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LoadingSpinner2)d)._activity?.Refresh();
        }

        private void UpdateAnimation()
        {
            if (!_activity.CanAnimate)
            {
                StopAnimation();
                return;
            }
            if (_clock != null) return;
            _clock = (ClockGroup)Pulse.CreateClock(true);
            for (int i = 0; i < DotCount; i++)
                Dots.Children[i].ApplyAnimationClock(UIElement.OpacityProperty,
                    (AnimationClock)_clock.Children[i], HandoffBehavior.SnapshotAndReplace);
        }

        private void StopAnimation()
        {
            if (_clock == null) return;
            _clock.Controller.Remove();
            foreach (UIElement dot in Dots.Children)
                dot.ApplyAnimationClock(UIElement.OpacityProperty, null);
            _clock = null;
        }
    }
}
