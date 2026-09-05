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
    /// 표시 전용 컨트롤. DataContext를 덮어쓰거나 Model을 직접 참조하지 않는다.
    /// 회전과 점의 배치는 View의 책임이므로 이 코드 비하인드에 둔다.
    /// </summary>
    public partial class LoadingSpinner : UserControl
    {
        public static readonly DependencyProperty IsActivateProperty = DependencyProperty.Register(
            nameof(IsActivate), typeof(bool), typeof(LoadingSpinner),
            new FrameworkPropertyMetadata(false, OnIsActivateChanged));

        public static readonly DependencyProperty DotBrushProperty = DependencyProperty.Register(
            nameof(DotBrush), typeof(Brush), typeof(LoadingSpinner),
            new FrameworkPropertyMetadata(Brushes.DimGray));

        private const int DotCount = 8;
        private const double Center = 32.0;
        private const double OrbitRadius = 22.0;
        private const double DotDiameter = 8.0;
        private const double OpacityGamma = 2.2;
        private static readonly DoubleAnimation Rotation = CreateRotation();
        private readonly SpinnerActivityMonitor _activity;
        private bool _isAnimating;

        public LoadingSpinner()
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
                double angle = -Math.PI / 2.0 + i * (2.0 * Math.PI / DotCount);
                double progress = (double)i / (DotCount - 1);
                var dot = new Ellipse
                {
                    Width = DotDiameter,
                    Height = DotDiameter,
                    // 감마 곡선으로 꼬리의 대비를 조절한다. 지각 밝기의 근사이며
                    // 실제 보이는 밝기는 DotBrush/Background와 디스플레이에 의존한다.
                    Opacity = 0.12 + 0.88 * Math.Pow(progress, OpacityGamma),
                    UseLayoutRounding = false,
                    SnapsToDevicePixels = false
                };
                dot.SetBinding(Shape.FillProperty, new Binding(nameof(DotBrush)) { Source = this });
                Canvas.SetLeft(dot, Center + OrbitRadius * Math.Cos(angle) - DotDiameter / 2.0);
                Canvas.SetTop(dot, Center + OrbitRadius * Math.Sin(angle) - DotDiameter / 2.0);
                Rotor.Children.Add(dot);
            }
        }

        private static void OnIsActivateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LoadingSpinner)d)._activity?.Refresh();
        }

        private void UpdateAnimation()
        {
            // 숨김/탭 전환/Unloaded 동안에는 애니메이션 clock을 유지하지 않는다.
            if (!_activity.CanAnimate)
            {
                StopAnimation();
                return;
            }

            if (_isAnimating) return;
            ((RotateTransform)Rotor.RenderTransform).BeginAnimation(
                RotateTransform.AngleProperty, Rotation, HandoffBehavior.SnapshotAndReplace);
            _isAnimating = true;
        }

        private static DoubleAnimation CreateRotation()
        {
            var rotation = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1.0)))
            {
                RepeatBehavior = RepeatBehavior.Forever
                // 가감속 없이 일정한 각속도. 360→0 경계는 동일한 위치다.
            };
            rotation.Freeze();
            return rotation;
        }

        private void StopAnimation()
        {
            if (Rotor == null || !_isAnimating) return;
            var transform = (RotateTransform)Rotor.RenderTransform;
            transform.BeginAnimation(RotateTransform.AngleProperty, null);
            transform.Angle = 0;
            _isAnimating = false;
        }
    }
}
