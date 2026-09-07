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
    /// Rotates the dot group clockwise by 45 degrees once per second, without interpolation.
    /// </summary>
    public partial class LoadingSpinner3 : UserControl
    {
        public static readonly DependencyProperty IsActivateProperty = DependencyProperty.Register(
            nameof(IsActivate), typeof(bool), typeof(LoadingSpinner3),
            new FrameworkPropertyMetadata(false, OnIsActivateChanged));

        public static readonly DependencyProperty DotBrushProperty = DependencyProperty.Register(
            nameof(DotBrush), typeof(Brush), typeof(LoadingSpinner3),
            new FrameworkPropertyMetadata(Brushes.DimGray));

        private const int DotCount = 8;
        private const double Center = 32.0;
        private const double OrbitRadius = 22.0;
        private const double DotDiameter = 8.0;
        private const double OpacityGamma = 2.2;
        private static readonly DoubleAnimationUsingKeyFrames Rotation = CreateRotation();
        private readonly SpinnerActivityMonitor _activity;
        private bool _isAnimating;

        public LoadingSpinner3()
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

        private static DoubleAnimationUsingKeyFrames CreateRotation()
        {
            var rotation = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromSeconds(DotCount)),
                RepeatBehavior = RepeatBehavior.Forever
            };
            // Hold each angle for a full second; 360 and 0 coincide at the repeat boundary.
            for (int step = 0; step <= DotCount; step++)
                rotation.KeyFrames.Add(new DiscreteDoubleKeyFrame(
                    step * (360.0 / DotCount), KeyTime.FromTimeSpan(TimeSpan.FromSeconds(step))));
            rotation.Freeze();
            return rotation;
        }

        private static void OnIsActivateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LoadingSpinner3)d)._activity?.Refresh();
        }

        private void UpdateAnimation()
        {
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
