using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AxamlMapControl.Source
{
    public partial class Map
    {
        public static readonly StyledProperty<bool> IsZoomAnimationEnabledProperty =
            AvaloniaProperty.Register<Map, bool>(nameof(IsZoomAnimationEnabled), false);
        public static readonly StyledProperty<TimeSpan> ZoomAnimationDurationProperty =
            AvaloniaProperty.Register<Map, TimeSpan>(nameof(ZoomAnimationDuration),
                TimeSpan.FromMilliseconds(200), coerce: (_, value) =>
                    value < TimeSpan.Zero ? TimeSpan.Zero :
                    value > TimeSpan.FromSeconds(2) ? TimeSpan.FromSeconds(2) : value);

        public bool IsZoomAnimationEnabled
        {
            get => GetValue(IsZoomAnimationEnabledProperty);
            set => SetValue(IsZoomAnimationEnabledProperty, value);
        }
        public TimeSpan ZoomAnimationDuration
        {
            get => GetValue(ZoomAnimationDurationProperty);
            set => SetValue(ZoomAnimationDurationProperty, value);
        }

        private readonly ZoomAnimationSurface _zoomSurface;
        private readonly DispatcherTimer _zoomTimer;
        private readonly Stopwatch _zoomClock = new();
        private RenderTargetBitmap? _zoomSnapshot;
        private bool _capturingZoomSnapshot;
        private bool _captureWithoutTiles;
        private bool _animateNewTiles;
        internal bool SuppressGraticuleRendering => _capturingZoomSnapshot || _zoomSnapshot is not null;
        internal bool SuppressTileCapture => _capturingZoomSnapshot && _captureWithoutTiles;
        private Size _snapshotSize;
        private Point _animationFocus;
        private Point _animationStartOrigin;
        private Point _snapshotBaseOrigin;
        private Point _visualOrigin;
        private double _animationStartZoom;
        private double _snapshotBaseZoom;
        private double _animationTargetZoom;
        private double _visualZoom;
        private double _snapshotScale = 1;
        private TimeSpan _animationDuration;

        private static void ConfigureZoomAnimationProperties()
        {
            IsZoomAnimationEnabledProperty.Changed.AddClassHandler<Map>((map, _) =>
            {
                if (!map.IsZoomAnimationEnabled) map.StopZoomAnimation();
            });
            ZoomAnimationDurationProperty.Changed.AddClassHandler<Map>((map, _) =>
            {
                if (map.ZoomAnimationDuration <= TimeSpan.Zero) map.StopZoomAnimation();
            });
        }

        private ZoomAnimationSurface CreateZoomAnimationSurface() => new ZoomAnimationSurface(this)
        {
            IsHitTestVisible = false,
            ClipToBounds = true,
            ZIndex = int.MaxValue
        };

        private DispatcherTimer CreateZoomAnimationTimer() =>
            new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };

        private void InitializeZoomAnimation()
        {
            VisualChildren.Add(_zoomSurface);
            _zoomTimer.Tick += (_, _) => AdvanceZoomAnimation();
            DetachedFromVisualTree += (_, _) => StopZoomAnimation();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var result = base.MeasureOverride(availableSize);
            _zoomSurface.Measure(availableSize);
            return result;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var result = base.ArrangeOverride(finalSize);
            _zoomSurface.Arrange(new Rect(finalSize));
            return result;
        }

        private void ZoomAt(int target, Point focus)
        {
            var startZoom = _zoomSnapshot is null ? ZoomLevel : _visualZoom;
            var startOrigin = _zoomSnapshot is null ? Transform.ViewCenter : _visualOrigin;
            var zoomingOut = target < ZoomLevel;
            // On zoom out capture live overlays at their logical level, without any old tile composite.
            var snapshotBaseZoom = zoomingOut ? ZoomLevel : startZoom;
            var snapshotBaseOrigin = zoomingOut ? Transform.ViewCenter : startOrigin;
            RenderTargetBitmap? snapshot = null;
            if (IsZoomAnimationEnabled && ZoomAnimationDuration > TimeSpan.Zero && _hasView &&
                Bounds.Width > 0 && Bounds.Height > 0)
            {
                try
                {
                    var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
                    snapshot = new RenderTargetBitmap(new PixelSize(
                        Math.Max(1, checked((int)Math.Ceiling(Bounds.Width * scaling))),
                        Math.Max(1, checked((int)Math.Ceiling(Bounds.Height * scaling)))),
                        new Vector(96 * scaling, 96 * scaling));
                    _captureWithoutTiles = zoomingOut;
                    _capturingZoomSnapshot = true;
                    try { snapshot.Render(this); }
                    finally
                    {
                        _capturingZoomSnapshot = false;
                        _captureWithoutTiles = false;
                    }
                }
                catch (Exception ex)
                {
                    snapshot?.Dispose();
                    snapshot = null;
                    Trace.TraceWarning("Animated zoom snapshot failed: {0}", ex);
                }
            }
            StopZoomAnimation();
            _zoomFocus = focus;
            try
            {
                SetCurrentValue(ZoomLevelProperty, target);
                if (snapshot is not null)
                {
                    var targetScale = Math.Pow(2, ZoomLevel - startZoom);
                    var targetOrigin = new Point(
                        focus.X + (startOrigin.X - focus.X) * targetScale,
                        focus.Y + (startOrigin.Y - focus.Y) * targetScale);
                    Transform.Translate(targetOrigin - Transform.ViewCenter);
                }
            }
            catch
            {
                snapshot?.Dispose();
                throw;
            }
            finally { _zoomFocus = null; }
            if (snapshot is not null)
            {
                _zoomSnapshot = snapshot;
                _animateNewTiles = zoomingOut;
                _snapshotSize = Bounds.Size;
                _animationFocus = focus;
                _animationStartOrigin = startOrigin;
                _snapshotBaseOrigin = snapshotBaseOrigin;
                _snapshotBaseZoom = snapshotBaseZoom;
                _visualOrigin = startOrigin;
                _animationStartZoom = startZoom;
                _animationTargetZoom = ZoomLevel;
                _visualZoom = startZoom;
                _snapshotScale = Math.Pow(2, startZoom - snapshotBaseZoom);
                _animationDuration = ZoomAnimationDuration;
                _zoomClock.Restart();
                _zoomTimer.Start();
                InvalidateAnimatedLayers();
                _zoomSurface.InvalidateVisual();
            }
        }

        internal bool TryGetTileAnimation(out double scale, out Point origin)
        {
            scale = Math.Pow(2, _visualZoom - _animationTargetZoom);
            origin = _visualOrigin;
            return _zoomSnapshot is not null && _animateNewTiles;
        }

        private void AdvanceZoomAnimation()
        {
            if (_zoomSnapshot is null || !IsZoomAnimationEnabled || _animationDuration <= TimeSpan.Zero)
            {
                StopZoomAnimation();
                return;
            }
            var progress = Math.Clamp(_zoomClock.Elapsed.TotalMilliseconds /
                _animationDuration.TotalMilliseconds, 0, 1);
            if (progress >= 1)
            {
                StopZoomAnimation();
                return;
            }
            var eased = 1 - Math.Pow(1 - progress, 3);
            _visualZoom = _animationStartZoom + (_animationTargetZoom - _animationStartZoom) * eased;
            var visualScale = Math.Pow(2, _visualZoom - _animationStartZoom);
            _snapshotScale = Math.Pow(2, _visualZoom - _snapshotBaseZoom);
            _visualOrigin = new Point(
                _animationFocus.X + (_animationStartOrigin.X - _animationFocus.X) * visualScale,
                _animationFocus.Y + (_animationStartOrigin.Y - _animationFocus.Y) * visualScale);
            _visualOrigin = Transform.ConstrainVerticalOrigin(_visualOrigin,
                Transform.GetScale(0) / Math.Pow(2, _visualZoom), LocalViewBounds);
            if (_animateNewTiles) InvalidateAnimatedLayers();
            _zoomSurface.InvalidateVisual();
        }

        private void StopZoomAnimation()
        {
            _zoomTimer?.Stop();
            _zoomClock.Stop();
            var snapshot = _zoomSnapshot;
            _zoomSnapshot = null;
            _animateNewTiles = false;
            _visualZoom = ZoomLevel;
            _visualOrigin = Transform.ViewCenter;
            snapshot?.Dispose();
            if (snapshot is not null) InvalidateAnimatedLayers();
            _zoomSurface?.InvalidateVisual();
        }

        private void InvalidateAnimatedLayers()
        {
            foreach (var visual in this.GetVisualDescendants())
            {
                if (visual is MapGraticule graticule && ReferenceEquals(graticule.MapParent, this))
                    graticule.InvalidateVisual();
                else if (visual is MapTileLayer layer && ReferenceEquals(layer.MapParent, this))
                    layer.InvalidateZoomAnimation();
            }
        }

        private void DrawZoomAnimation(DrawingContext context)
        {
            if (_zoomSnapshot is not { } snapshot || SuppressTileCapture) return;
            var scale = _snapshotScale;
            var destination = new Rect(_visualOrigin.X - _snapshotBaseOrigin.X * scale,
                _visualOrigin.Y - _snapshotBaseOrigin.Y * scale,
                _snapshotSize.Width * scale, _snapshotSize.Height * scale);
            using (context.PushClip(LocalViewBounds))
            {
                context.DrawImage(snapshot, destination);
                if (_capturingZoomSnapshot) return;
                var vectorScale = Math.Pow(2, _visualZoom - _animationTargetZoom);
                foreach (var visual in this.GetVisualDescendants())
                {
                    if (visual is not MapGraticule graticule ||
                        !ReferenceEquals(graticule.MapParent, this) || !graticule.IsEffectivelyVisible) continue;
                    var opacity = 1d;
                    for (Visual? ancestor = graticule; ancestor is not null && ancestor != this;
                         ancestor = ancestor.GetVisualParent())
                        opacity *= ancestor.Opacity;
                    if (opacity <= 0) continue;
                    using (context.PushOpacity(opacity))
                        graticule.RenderAnimated(context, vectorScale, _visualOrigin);
                }
            }
        }

        private sealed class ZoomAnimationSurface : Control
        {
            private readonly Map _owner;
            public ZoomAnimationSurface(Map owner) => _owner = owner;
            public override void Render(DrawingContext context) => _owner.DrawZoomAnimation(context);
        }
    }
}
