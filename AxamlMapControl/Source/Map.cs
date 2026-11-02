using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
            AvaloniaProperty.Register<Map, int>(nameof(ZoomLevel), 0, coerce: (owner, value) =>
            {
                var map = (Map)owner;
                return Math.Min(Math.Max(value, map.MinZoomLevel), Math.Max(map.MinZoomLevel, map.MaxZoomLevel));
            });
        public static readonly StyledProperty<MapTileLayer> TileLayerProperty =
            AvaloniaProperty.Register<Map, MapTileLayer>(nameof(TileLayer));
        public static readonly StyledProperty<Proj.Location> CenterProperty =
            AvaloniaProperty.Register<Map, Proj.Location>(nameof(Center));
        public static readonly StyledProperty<bool> IsZoomAnimationEnabledProperty =
            AvaloniaProperty.Register<Map, bool>(nameof(IsZoomAnimationEnabled), false);
        public static readonly StyledProperty<TimeSpan> ZoomAnimationDurationProperty =
            AvaloniaProperty.Register<Map, TimeSpan>(nameof(ZoomAnimationDuration), TimeSpan.FromMilliseconds(200),
                coerce: (_, value) => value < TimeSpan.Zero ? TimeSpan.Zero :
                    value > TimeSpan.FromSeconds(2) ? TimeSpan.FromSeconds(2) : value);

        public Proj.Location Center { get => GetValue(CenterProperty); set => SetValue(CenterProperty, value); }
        public int MinZoomLevel { get => GetValue(MinZoomLevelProperty); set => SetValue(MinZoomLevelProperty, value); }
        public int MaxZoomLevel { get => GetValue(MaxZoomLevelProperty); set => SetValue(MaxZoomLevelProperty, value); }
        public int ZoomLevel { get => GetValue(ZoomLevelProperty); set => SetValue(ZoomLevelProperty, value); }
        public MapTileLayer TileLayer { get => GetValue(TileLayerProperty); set => SetValue(TileLayerProperty, value); }
        public bool IsZoomAnimationEnabled { get => GetValue(IsZoomAnimationEnabledProperty); set => SetValue(IsZoomAnimationEnabledProperty, value); }
        public TimeSpan ZoomAnimationDuration { get => GetValue(ZoomAnimationDurationProperty); set => SetValue(ZoomAnimationDurationProperty, value); }

        public ViewTransform Transform { get; } = new();
        private readonly MapZoomAnimator _zoomAnimator;
        private Point _mousePosition;
        private bool _hasView;
        private bool _applyingZoom;
        private Rect LocalViewBounds => new Rect(0, 0, Bounds.Width, Bounds.Height);
        internal bool SuppressGraticuleRendering => _zoomAnimator?.SuppressGraticuleRendering ?? false;
        internal bool SuppressTileCapture => _zoomAnimator?.SuppressTileCapture ?? false;

        static Map()
        {
            ClipToBoundsProperty.OverrideMetadata<Map>(new StyledPropertyMetadata<bool>(true));
            BackgroundProperty.OverrideMetadata<Map>(new StyledPropertyMetadata<IBrush>());
            TileLayerProperty.Changed.AddClassHandler<Map>((map, e) =>
            {
                map._zoomAnimator?.Stop();
                if (e.OldValue is MapTileLayer oldLayer) map.Children.Remove(oldLayer);
                if (e.NewValue is MapTileLayer newLayer) map.Children.Insert(0, newLayer);
            });
            CenterProperty.Changed.AddClassHandler<Map>((map, _) => map.UpdateCenter());
            BoundsProperty.Changed.AddClassHandler<Map>((map, _) => map.UpdateBounds());
            ZoomLevelProperty.Changed.AddClassHandler<Map>((map, _) => map.UpdateZoom());
            IsZoomAnimationEnabledProperty.Changed.AddClassHandler<Map>((map, _) =>
            {
                if (!map.IsZoomAnimationEnabled) map._zoomAnimator?.Stop();
            });
            ZoomAnimationDurationProperty.Changed.AddClassHandler<Map>((map, _) =>
            {
                if (map.ZoomAnimationDuration <= TimeSpan.Zero) map._zoomAnimator?.Stop();
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
            _zoomAnimator = new MapZoomAnimator(new ZoomAnimationHost(this));
            VisualChildren.Add(_zoomAnimator.Surface);
            DetachedFromVisualTree += (_, _) => _zoomAnimator.Stop();
            Center = new Proj.Location();
            TileLayer = new MapTileLayer();
            PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
                _zoomAnimator.Stop();
                _mousePosition = e.GetPosition(this);
                e.Pointer.Capture(this);
            };
            PointerReleased += (_, e) =>
            {
                if (e.Pointer.Captured == this && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
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
            _zoomAnimator.Surface.Measure(availableSize);
            return result;
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            var result = base.ArrangeOverride(finalSize);
            _zoomAnimator.Surface.Arrange(new Rect(finalSize));
            return result;
        }
        private void HandleWheel(object? sender, PointerWheelEventArgs e)
        {
            var step = Math.Sign(e.Delta.Y);
            if (step == 0) return;
            var target = Math.Clamp(ZoomLevel + step, MinZoomLevel, Math.Max(MinZoomLevel, MaxZoomLevel));
            if (target == ZoomLevel) return;
            ZoomAt(target, e.GetPosition(this));
            e.Handled = true;
        }
        internal void ZoomAt(int target, Point focus) =>
            _zoomAnimator.ZoomTo(target, focus, IsZoomAnimationEnabled, ZoomAnimationDuration);

        private void ApplyZoom(int target, Point focus)
        {
            _applyingZoom = true;
            try { SetCurrentValue(ZoomLevelProperty, target); }
            finally { _applyingZoom = false; }
            ApplyViewZoom(focus);
        }
        private void ApplyViewZoom(Point focus)
        {
            if (!(Bounds.Width > 0 && Bounds.Height > 0)) return;
            if (!_hasView) UpdateCenter();
            else Transform.SetZoom(focus, ZoomLevel, LocalViewBounds);
        }
        private void UpdateCenter()
        {
            _zoomAnimator?.Stop();
            if (!(Bounds.Width > 0 && Bounds.Height > 0) || Center is not { } center) return;
            Transform.SetZoom(center, ZoomLevel, LocalViewBounds);
            _hasView = true;
        }
        private void UpdateBounds()
        {
            _zoomAnimator?.Stop();
            if (!(Bounds.Width > 0 && Bounds.Height > 0)) return;
            if (!_hasView) UpdateCenter();
            else Transform.Resize(LocalViewBounds);
        }
        private void UpdateZoom()
        {
            if (_applyingZoom) return;
            _zoomAnimator?.Stop();
            ApplyViewZoom(LocalViewBounds.Center);
        }
        internal bool TryGetTileAnimation(out double scale, out Point origin) =>
            _zoomAnimator.TryGetTileAnimation(out scale, out origin);

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
        private void DrawAnimatedOverlays(DrawingContext context, double scale, Point origin)
        {
            foreach (var visual in this.GetVisualDescendants())
            {
                if (visual is not MapGraticule graticule ||
                    !ReferenceEquals(graticule.MapParent, this) || !graticule.IsEffectivelyVisible) continue;
                var opacity = 1d;
                for (Visual? ancestor = graticule; ancestor is not null && ancestor != this;
                     ancestor = ancestor.GetVisualParent())
                    opacity *= ancestor.Opacity;
                if (opacity <= 0) continue;
                using (context.PushOpacity(opacity)) graticule.RenderAnimated(context, scale, origin);
            }
        }

        private sealed class ZoomAnimationHost : IMapZoomAnimationHost
        {
            private readonly Map _map;
            public ZoomAnimationHost(Map map) => _map = map;
            public int ZoomLevel => _map.ZoomLevel;
            public Rect ViewBounds => _map.LocalViewBounds;
            public Point ViewCenter => _map.Transform.ViewCenter;
            public bool HasView => _map._hasView;
            public RenderTargetBitmap CaptureSnapshot()
            {
                var scaling = TopLevel.GetTopLevel(_map)?.RenderScaling ?? 1;
                var snapshot = new RenderTargetBitmap(new PixelSize(
                    Math.Max(1, checked((int)Math.Ceiling(_map.Bounds.Width * scaling))),
                    Math.Max(1, checked((int)Math.Ceiling(_map.Bounds.Height * scaling)))),
                    new Vector(96 * scaling, 96 * scaling));
                try { snapshot.Render(_map); return snapshot; }
                catch { snapshot.Dispose(); throw; }
            }
            public void ApplyZoom(int target, Point focus) => _map.ApplyZoom(target, focus);
            public void AlignViewOrigin(Point origin) => _map.Transform.Translate(origin - ViewCenter);
            public Point ConstrainOrigin(Point origin, double zoom) =>
                _map.Transform.ConstrainVerticalOrigin(origin, _map.Transform.GetScale(0) / Math.Pow(2, zoom), ViewBounds);
            public void InvalidateLayers() => _map.InvalidateAnimatedLayers();
            public void DrawOverlays(DrawingContext context, double scale, Point origin) =>
                _map.DrawAnimatedOverlays(context, scale, origin);
        }
    }
}
