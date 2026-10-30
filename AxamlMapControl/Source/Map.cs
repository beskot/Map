using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

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
        public ViewTransform Transform { get; } = new();
        private Point _mousePosition;
        private Point? _zoomFocus;
        private bool _hasView;
        private Rect LocalViewBounds => new Rect(0, 0, Bounds.Width, Bounds.Height);

        static Map()
        {
            ClipToBoundsProperty.OverrideMetadata<Map>(new StyledPropertyMetadata<bool>(true));
            BackgroundProperty.OverrideMetadata<Map>(new StyledPropertyMetadata<IBrush>());
            TileLayerProperty.Changed.AddClassHandler<Map>((map, e) =>
            {
                if (e.OldValue is MapTileLayer oldLayer) map.Children.Remove(oldLayer);
                if (e.NewValue is MapTileLayer newLayer) map.Children.Insert(0, newLayer);
            });
            CenterProperty.Changed.AddClassHandler<Map>((map, _) => map.UpdateCenter());
            BoundsProperty.Changed.AddClassHandler<Map>((map, _) => map.UpdateBounds());
            ZoomLevelProperty.Changed.AddClassHandler<Map>((map, _) => map.UpdateZoom());
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
            Center = new Proj.Location();
            TileLayer = new MapTileLayer();
            PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
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
            PointerWheelChanged += (_, e) =>
            {
                _zoomFocus = e.GetPosition(this);
                try { ZoomLevel += Math.Sign(e.Delta.Y); }
                finally { _zoomFocus = null; }
            };
        }

        private void UpdateCenter()
        {
            if (!(Bounds.Width > 0 && Bounds.Height > 0)) return;
            if (Center is not { } center) return;
            Transform.SetZoom(center, ZoomLevel, LocalViewBounds);
            _hasView = true;
        }

        private void UpdateBounds()
        {
            if (!(Bounds.Width > 0 && Bounds.Height > 0)) return;
            if (!_hasView) UpdateCenter();
            else Transform.Resize(LocalViewBounds);
        }

        private void UpdateZoom()
        {
            if (!(Bounds.Width > 0 && Bounds.Height > 0)) return;
            if (!_hasView) UpdateCenter();
            else Transform.SetZoom(_zoomFocus ?? LocalViewBounds.Center, ZoomLevel, LocalViewBounds);
        }
    }
}
