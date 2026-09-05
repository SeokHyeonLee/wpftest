using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RefDataMvvm.Wpf.Controls
{
    /// <summary>
    /// 폴링/Rendering/LayoutUpdated 없이 필요한 표시 상태 이벤트만 관찰한다.
    /// Unloaded에서 Window/ScrollViewer 구독을 모두 해제한다.
    /// </summary>
    internal sealed class SpinnerActivityMonitor
    {
        private readonly FrameworkElement _owner;
        private readonly Func<bool> _isActive;
        private readonly Action _changed;
        private readonly List<ScrollViewer> _scrollViewers = new List<ScrollViewer>();
        private readonly List<ScrollContentPresenter> _viewports = new List<ScrollContentPresenter>();
        private Window _window;

        public SpinnerActivityMonitor(FrameworkElement owner, Func<bool> isActive, Action changed)
        {
            _owner = owner;
            _isActive = isActive;
            _changed = changed;
            owner.Loaded += OnLoaded;
            owner.Unloaded += OnUnloaded;
            owner.IsVisibleChanged += OnVisibilityChanged;
            owner.SizeChanged += OnSizeChanged;
        }

        public bool CanAnimate { get; private set; }

        public void Refresh()
        {
            bool next = IsInVisibleViewport();
            if (CanAnimate == next) return;
            CanAnimate = next;
            _changed();
        }

        private bool IsInVisibleViewport()
        {
            if (!_isActive() || !_owner.IsLoaded || !_owner.IsVisible ||
                _owner.ActualWidth <= 0 || _owner.ActualHeight <= 0 ||
                (_window != null && (!_window.IsVisible || _window.WindowState == WindowState.Minimized)))
                return false;

            // 중첩 ScrollViewer에서도 모든 viewport와 겹치는 부분이 있어야 실행한다.
            Rect visible = new Rect(_owner.RenderSize);
            foreach (var viewport in _viewports)
            {
                if (!viewport.IsAncestorOf(_owner)) return false;
                GeneralTransform toOwner = _owner.TransformToAncestor(viewport).Inverse;
                if (toOwner == null) return false;
                visible.Intersect(toOwner.TransformBounds(new Rect(viewport.RenderSize)));
                if (visible.IsEmpty || visible.Width <= 0 || visible.Height <= 0) return false;
            }
            return true;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            DetachAncestors();
            _window = Window.GetWindow(_owner);
            if (_window != null)
            {
                _window.StateChanged += OnWindowStateChanged;
                _window.IsVisibleChanged += OnVisibilityChanged;
            }
            for (var parent = VisualTreeHelper.GetParent(_owner); parent != null;
                 parent = VisualTreeHelper.GetParent(parent))
            {
                var scrollViewer = parent as ScrollViewer;
                if (scrollViewer != null)
                {
                    _scrollViewers.Add(scrollViewer);
                    scrollViewer.ScrollChanged += OnScrollChanged;
                }
                var viewport = parent as ScrollContentPresenter;
                if (viewport != null)
                {
                    _viewports.Add(viewport);
                    viewport.SizeChanged += OnSizeChanged;
                }
            }
            Refresh();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DetachAncestors();
            if (!CanAnimate) return;
            CanAnimate = false;
            _changed();
        }

        private void DetachAncestors()
        {
            if (_window != null)
            {
                _window.StateChanged -= OnWindowStateChanged;
                _window.IsVisibleChanged -= OnVisibilityChanged;
                _window = null;
            }
            foreach (var scrollViewer in _scrollViewers) scrollViewer.ScrollChanged -= OnScrollChanged;
            foreach (var viewport in _viewports) viewport.SizeChanged -= OnSizeChanged;
            _scrollViewers.Clear();
            _viewports.Clear();
        }

        private void OnWindowStateChanged(object sender, EventArgs e) { Refresh(); }
        private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) { Refresh(); }
        private void OnSizeChanged(object sender, SizeChangedEventArgs e) { Refresh(); }
        private void OnScrollChanged(object sender, ScrollChangedEventArgs e) { Refresh(); }
    }
}
