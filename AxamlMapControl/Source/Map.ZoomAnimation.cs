using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace AxamlMapControl.Source
{
    internal interface IMapZoomAnimationHost
    {
        int ZoomLevel { get; }
        Rect ViewBounds { get; }
        Point ViewCenter { get; }
        bool HasView { get; }
        RenderTargetBitmap CaptureSnapshot();
        void ApplyZoom(int target, Point focus);
        void AlignViewOrigin(Point origin);
        Point ConstrainOrigin(Point origin, double zoom);
        void InvalidateLayers();
        void DrawOverlays(DrawingContext context, double scale, Point origin);
    }

    /// <summary>Owns animation resources and state; has no dependency on the Map class.</summary>
    internal sealed class MapZoomAnimator : IDisposable
    {
        private readonly IMapZoomAnimationHost _host;
        private readonly DispatcherTimer _timer;
        private readonly Stopwatch _clock = new();
        private RenderTargetBitmap? _snapshot;
        private bool _capturing;
        private bool _captureWithoutTiles;
        private bool _animateNewTiles;
        private bool _disposed;
        private Size _snapshotSize;
        private Point _focus;
        private Point _startOrigin;
        private Point _snapshotOrigin;
        private Point _visualOrigin;
        private double _startZoom;
        private double _snapshotZoom;
        private double _targetZoom;
        private double _visualZoom;
        private double _snapshotScale = 1;
        private TimeSpan _duration;

        public Control Surface { get; }
        public bool SuppressGraticuleRendering => _capturing || _snapshot is not null;
        public bool SuppressTileCapture => _capturing && _captureWithoutTiles;

        public MapZoomAnimator(IMapZoomAnimationHost host)
        {
            _host = host;
            Surface = new AnimationSurface(this)
            {
                IsHitTestVisible = false, ClipToBounds = true, ZIndex = int.MaxValue
            };
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += OnTick;
        }

        public void ZoomTo(int target, Point focus, bool enabled, TimeSpan duration)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MapZoomAnimator));
            if (target == _host.ZoomLevel) return;
            var startZoom = _snapshot is null ? _host.ZoomLevel : _visualZoom;
            var startOrigin = _snapshot is null ? _host.ViewCenter : _visualOrigin;
            var zoomingOut = target < _host.ZoomLevel;
            var snapshotZoom = zoomingOut ? _host.ZoomLevel : startZoom;
            var snapshotOrigin = zoomingOut ? _host.ViewCenter : startOrigin;
            RenderTargetBitmap? snapshot = null;
            if (enabled && duration > TimeSpan.Zero && _host.HasView &&
                _host.ViewBounds.Width > 0 && _host.ViewBounds.Height > 0)
            {
                try
                {
                    _captureWithoutTiles = zoomingOut;
                    _capturing = true;
                    try { snapshot = _host.CaptureSnapshot(); }
                    finally { _capturing = false; _captureWithoutTiles = false; }
                }
                catch (Exception ex)
                {
                    snapshot?.Dispose();
                    snapshot = null;
                    Trace.TraceWarning("Animated zoom snapshot failed: {0}", ex);
                }
            }
            Stop();
            try
            {
                _host.ApplyZoom(target, focus);
                if (snapshot is not null)
                {
                    var targetScale = Math.Pow(2, _host.ZoomLevel - startZoom);
                    _host.AlignViewOrigin(new Point(
                        focus.X + (startOrigin.X - focus.X) * targetScale,
                        focus.Y + (startOrigin.Y - focus.Y) * targetScale));
                }
            }
            catch { snapshot?.Dispose(); throw; }
            if (snapshot is null) return;
            _snapshot = snapshot;
            _animateNewTiles = zoomingOut;
            _snapshotSize = _host.ViewBounds.Size;
            _focus = focus;
            _startOrigin = startOrigin;
            _snapshotOrigin = snapshotOrigin;
            _snapshotZoom = snapshotZoom;
            _visualOrigin = startOrigin;
            _startZoom = startZoom;
            _targetZoom = _host.ZoomLevel;
            _visualZoom = startZoom;
            _snapshotScale = Math.Pow(2, startZoom - snapshotZoom);
            _duration = duration;
            _clock.Restart();
            _timer.Start();
            _host.InvalidateLayers();
            Surface.InvalidateVisual();
        }

        public bool TryGetTileAnimation(out double scale, out Point origin)
        {
            scale = Math.Pow(2, _visualZoom - _targetZoom);
            origin = _visualOrigin;
            return _snapshot is not null && _animateNewTiles;
        }
        private void OnTick(object? sender, EventArgs e) => Advance();
        private void Advance()
        {
            if (_snapshot is null || _duration <= TimeSpan.Zero) { Stop(); return; }
            var progress = Math.Clamp(_clock.Elapsed.TotalMilliseconds / _duration.TotalMilliseconds, 0, 1);
            if (progress >= 1) { Stop(); return; }
            var eased = 1 - Math.Pow(1 - progress, 3);
            _visualZoom = _startZoom + (_targetZoom - _startZoom) * eased;
            var scale = Math.Pow(2, _visualZoom - _startZoom);
            _snapshotScale = Math.Pow(2, _visualZoom - _snapshotZoom);
            _visualOrigin = _host.ConstrainOrigin(new Point(
                _focus.X + (_startOrigin.X - _focus.X) * scale,
                _focus.Y + (_startOrigin.Y - _focus.Y) * scale), _visualZoom);
            if (_animateNewTiles) _host.InvalidateLayers();
            Surface.InvalidateVisual();
        }
        public void Stop()
        {
            _timer.Stop();
            _clock.Stop();
            var snapshot = _snapshot;
            _snapshot = null;
            _animateNewTiles = false;
            _visualZoom = _host.ZoomLevel;
            _visualOrigin = _host.ViewCenter;
            snapshot?.Dispose();
            if (snapshot is not null) _host.InvalidateLayers();
            Surface.InvalidateVisual();
        }
        private void Draw(DrawingContext context)
        {
            if (_snapshot is not { } snapshot || SuppressTileCapture) return;
            var destination = new Rect(
                _visualOrigin.X - _snapshotOrigin.X * _snapshotScale,
                _visualOrigin.Y - _snapshotOrigin.Y * _snapshotScale,
                _snapshotSize.Width * _snapshotScale, _snapshotSize.Height * _snapshotScale);
            using (context.PushClip(_host.ViewBounds))
            {
                context.DrawImage(snapshot, destination);
                if (!_capturing)
                    _host.DrawOverlays(context, Math.Pow(2, _visualZoom - _targetZoom), _visualOrigin);
            }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Tick -= OnTick;
            Stop();
        }
        private sealed class AnimationSurface : Control
        {
            private readonly MapZoomAnimator _owner;
            public AnimationSurface(MapZoomAnimator owner) => _owner = owner;
            public override void Render(DrawingContext context) => _owner.Draw(context);
        }
    }
}
