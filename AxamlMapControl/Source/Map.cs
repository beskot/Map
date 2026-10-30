using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AxamlMapControl.Source
{
    public class Map : Panel
    {
        public static readonly StyledProperty<int> MinZoomLevelProperty =
            AvaloniaProperty.Register<Map, int>(nameof(MinZoomLevel), 0,
                coerce: (_, value) => Math.Min(30, Math.Max(0, value)));
        public static readonly StyledProperty<int> MaxZoomLevelProperty =
            AvaloniaProperty.Register<Map, int>(nameof(MaxZoomLevel), 20,
                coerce: (_, value) => Math.Min(30, Math.Max(0, value)));
        public static readonly StyledProperty<int> ZoomLevelProperty =
            AvaloniaProperty.Register<Map, int>(nameof(ZoomLevel), 0,
                coerce: (owner, value) =>
                {
                    var map = (Map)owner;
                    return Math.Min(Math.Max(value, map.MinZoomLevel),
                        Math.Max(map.MinZoomLevel, map.MaxZoomLevel));
                });
        public static readonly StyledProperty<MapTileLayer> TileLayerProperty =
            AvaloniaProperty.Register<Map, MapTileLayer>(nameof(TileLayer));
        public static readonly StyledProperty<Proj.Location> CenterProperty =
            AvaloniaProperty.Register<Map, Proj.Location>(nameof(Center));
        public static readonly StyledProperty<bool> IsZoomAnimationEnabledProperty =
            AvaloniaProperty.Register<Map, bool>(nameof(IsZoomAnimationEnabled), false);
        public static readonly StyledProperty<TimeSpan> ZoomAnimationDurationProperty =
            AvaloniaProperty.Register<Map, TimeSpan>(nameof(ZoomAnimationDuration),
                TimeSpan.FromMilliseconds(200), coerce: (_, value) =>
                    value < TimeSpan.Zero ? TimeSpan.Zero :
                    value > TimeSpan.FromSeconds(2) ? TimeSpan.FromSeconds(2) : value);

        public Proj.Location Center
        {
            get => GetValue(CenterProperty);
            set => SetValue(CenterProperty, value);
        }
        public int MinZoomLevel
        {
            get => GetValue(MinZoomLevelProperty);
            set => SetValue(MinZoomLevelProperty, value);
        }
        public int MaxZoomLevel
        {
            get => GetValue(MaxZoomLevelProperty);
            set => SetValue(MaxZoomLevelProperty, value);
        }
        public int ZoomLevel
        {
            get => GetValue(ZoomLevelProperty);
            set => SetValue(ZoomLevelProperty, value);
        }
        public MapTileLayer TileLayer
        {
            get => GetValue(TileLayerProperty);
            set => SetValue(TileLayerProperty, value);
        }
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

        public ViewTransform Transform { get; } = new();
        private Point _mousePosition;
        private Point? _zoomFocus;
        private bool _hasView;
        private Rect LocalViewBounds => new Rect(0, 0, Bounds.Width, Bounds.Height);
        private readonly ZoomAnimationSurface _zoomSurface;
        private readonly DispatcherTimer _zoomTimer;
        private readonly Stopwatch _zoomClock = new();
        private RenderTargetBitmap? _zoomSnapshot;
        private bool _capturingZoomSnapshot;
        internal bool SuppressGraticuleRendering => _capturingZoomSnapshot || _zoomSnapshot is not null;
        private Size _snapshotSize;
        private Point _animationFocus;
        private Point _animationStartOrigin;
        private Point _visualOrigin;
        private double _animationStartZoom;
        private double _animationTargetZoom;
        private double _visualZoom;
        private double _snapshotScale = 1;
        private TimeSpan _animationDuration;

        static Map()
        {
            ClipToBoundsProperty.OverrideMetadata<Map>(new StyledPropertyMetadata<bool>(true));
            BackgroundProperty.OverrideMetadata<Map>(new StyledPropertyMetadata<IBrush>());
            TileLayerProperty.Changed.AddClassHandler<Map>((map, e) =>
            {
                map.StopZoomAnimation();
                if (e.OldValue is MapTileLayer oldLayer) map.Children.Remove(oldLayer);
                if (e.NewValue is MapTileLayer newLayer) map.Children.Insert(0, newLayer);
            });
            CenterProperty.Changed.AddClassHandler<Map>((map, _) => map.UpdateCenter());
            BoundsProperty.Changed.AddClassHandler<Map>((map, _) => map.UpdateBounds());
            ZoomLevelProperty.Changed.AddClassHandler<Map>((map, _) => map.UpdateZoom());
            IsZoomAnimationEnabledProperty.Changed.AddClassHandler<Map>((map, _) =>
            {
                if (!map.IsZoomAnimationEnabled) map.StopZoomAnimation();
            });
            ZoomAnimationDurationProperty.Changed.AddClassHandler<Map>((map, _) =>
            {
                if (map.ZoomAnimationDuration <= TimeSpan.Zero) map.StopZoomAnimation();
            });
            MinZoomLevelProperty.Changed.AddClassHandler<Map>((map, _) =>
            {
                if (map.MaxZoomLevel < map.MinZoomLevel) map.MaxZoomLevel = map.MinZoomLevel;
                map.CoerceValue(ZoomLevelProperty);
            });
            MaxZoomLevelProperty.Changed.AddClassHandler<Map>((map, _) =>
            {
                if (map.MinZoomLevel > map.MaxZoomLevel) map.MinZoomLevel = map.MaxZoomLevel;
                map.CoerceValue(ZoomLevelProperty);
            });
        }

        public Map()
        {
            _zoomSurface = new ZoomAnimationSurface(this)
            {
                IsHitTestVisible = false,
                ClipToBounds = true,
                ZIndex = int.MaxValue
            };
            VisualChildren.Add(_zoomSurface);
            _zoomTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _zoomTimer.Tick += (_, _) => AdvanceZoomAnimation();
            DetachedFromVisualTree += (_, _) => StopZoomAnimation();
            Center = new Proj.Location();
            TileLayer = new MapTileLayer();
            PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
                StopZoomAnimation();
                _mousePosition = e.GetPosition(this);
                e.Pointer.Capture(this);
            };
            PointerReleased += (_, e) =>
            {
                if (e.Pointer.Captured == this &&
                    !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    e.Pointer.Capture(null);
            };
            PointerMoved += (_, e) =>
            {
                if (e.Pointer.Captured != this) return;
                if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    e.Pointer.Capture(null);
                    return;
                }
                var position = e.GetPosition(this);
                Transform.Translate(position - _mousePosition);
                _mousePosition = position;
            };
            PointerWheelChanged += HandleWheel;
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

        private void HandleWheel(object? sender, PointerWheelEventArgs e)
        {
            var step = Math.Sign(e.Delta.Y);
            if (step == 0) return;
            var target = Math.Clamp(ZoomLevel + step, MinZoomLevel, Math.Max(MinZoomLevel, MaxZoomLevel));
            if (target == ZoomLevel) return;
            var focus = e.GetPosition(this);
            var startZoom = _zoomSnapshot is null ? ZoomLevel : _visualZoom;
            var startOrigin = _zoomSnapshot is null ? Transform.ViewCenter : _visualOrigin;
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
                    // Capture tiles and shapes, but never rasterize the vector graticule.
                    _capturingZoomSnapshot = true;
                    try { snapshot.Render(this); }
                    finally { _capturingZoomSnapshot = false; }
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
                // Start target-level tile requests immediately, not at animation completion.
                SetCurrentValue(ZoomLevelProperty, target);
                if (snapshot is not null)
                {
                    // Anchor to the visible frame, not the previous logical target.
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
                _snapshotSize = Bounds.Size;
                _animationFocus = focus;
                _animationStartOrigin = startOrigin;
                _visualOrigin = startOrigin;
                _animationStartZoom = startZoom;
                _animationTargetZoom = ZoomLevel;
                _visualZoom = startZoom;
                _snapshotScale = 1;
                _animationDuration = ZoomAnimationDuration;
                _zoomClock.Restart();
                _zoomTimer.Start();
                InvalidateGraticules();
                _zoomSurface.InvalidateVisual();
            }
            e.Handled = true;
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
            _snapshotScale = Math.Pow(2, _visualZoom - _animationStartZoom);
            _visualOrigin = new Point(
                _animationFocus.X + (_animationStartOrigin.X - _animationFocus.X) * _snapshotScale,
                _animationFocus.Y + (_animationStartOrigin.Y - _animationFocus.Y) * _snapshotScale);
            _zoomSurface.InvalidateVisual();
        }

        private void StopZoomAnimation()
        {
            _zoomTimer?.Stop();
            _zoomClock.Stop();
            var snapshot = _zoomSnapshot;
            _zoomSnapshot = null;
            _visualZoom = ZoomLevel;
            _visualOrigin = Transform.ViewCenter;
            snapshot?.Dispose();
            if (snapshot is not null) InvalidateGraticules();
            _zoomSurface?.InvalidateVisual();
        }

        private void InvalidateGraticules()
        {
            foreach (var visual in this.GetVisualDescendants())
                if (visual is MapGraticule graticule && ReferenceEquals(graticule.MapParent, this))
                    graticule.InvalidateVisual();
        }

        private void DrawZoomAnimation(DrawingContext context)
        {
            if (_zoomSnapshot is not { } snapshot) return;
            var scale = _snapshotScale;
            var destination = new Rect(_animationFocus.X * (1 - scale),
                _animationFocus.Y * (1 - scale), _snapshotSize.Width * scale, _snapshotSize.Height * scale);
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

        private void UpdateCenter()
        {
            StopZoomAnimation();
            if (!(Bounds.Width > 0 && Bounds.Height > 0)) return;
            if (Center is not { } center) return;
            Transform.SetZoom(center, ZoomLevel, LocalViewBounds);
            _hasView = true;
        }

        private void UpdateBounds()
        {
            StopZoomAnimation();
            if (!(Bounds.Width > 0 && Bounds.Height > 0)) return;
            if (!_hasView) UpdateCenter();
            else Transform.Resize(LocalViewBounds);
        }

        private void UpdateZoom()
        {
            StopZoomAnimation();
            if (!(Bounds.Width > 0 && Bounds.Height > 0)) return;
            if (!_hasView) UpdateCenter();
            else Transform.SetZoom(_zoomFocus ?? LocalViewBounds.Center, ZoomLevel, LocalViewBounds);
        }
    }
}
