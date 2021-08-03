using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AxamlMapControl.Source
{
    /// <summary>
    /// Map control.
    /// </summary>
    public class Map : Panel
    {
        public static readonly StyledProperty<int> MinZoomLevelProperty
            = AvaloniaProperty.Register<Map, int>(nameof(MinZoomLevel), 0);

        public static readonly StyledProperty<int> MaxZoomLevelProperty
            = AvaloniaProperty.Register<Map, int>(nameof(MaxZoomLevel), 20);

        public static readonly StyledProperty<int> ZoomLevelProperty
            = AvaloniaProperty.Register<Map, int>(nameof(ZoomLevel), 0,
             coerce: (o, val) =>
            {
                var ret = val;
                if (o is Map map)
                {
                    ret = Math.Min(Math.Max(val, map.MinZoomLevel), map.MaxZoomLevel);
                }

                return ret;
            });

        public static readonly StyledProperty<MapTileLayer> TileLayerProperty
            = AvaloniaProperty.Register<Map, MapTileLayer>(nameof(TileLayer),
                 new MapTileLayer());

        public static readonly StyledProperty<Proj.Location> CenterProperty
            = AvaloniaProperty.Register<Map, Proj.Location>(nameof(Center), new());


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

        public ViewTransform Transform { get; private set; } = new();

        private Point _mousePosition;

        static Map()
        {
            ClipToBoundsProperty.OverrideMetadata<Map>(new StyledPropertyMetadata<bool>(true));
            BackgroundProperty.OverrideMetadata<Map>(new StyledPropertyMetadata<IBrush>());
        }

        public Map()
        {
            PointerPressed += (o, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    _mousePosition = e.GetPosition(this);
                    e.Pointer.Capture(this);
                }
            };
            PointerReleased += (o, e) =>
            {
                if (e.Pointer.Captured == this && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    e.Pointer.Capture(null);
                }
            };
            PointerWheelChanged += (o, e) =>
            {
                ZoomLevel += Math.Sign(e.Delta.Y);
                Transform.SetZoom(e.GetPosition(this), ZoomLevel, Bounds);
            };
            PointerMoved += (o, e) =>
            {
                if (e.Pointer.Captured == this)
                {
                    var pos = e.GetPosition(this);
                    Transform.Translate(pos - _mousePosition);
                    _mousePosition = pos;
                }
            };

            TileLayerProperty.Changed.AddClassHandler<Map>((o, e) => o.MapTileLayerChange(e));
            CenterProperty.Changed.AddClassHandler<Map>((o, e) => o.CenterChange(e));
            BoundsProperty.Changed.AddClassHandler<Map>((o, e) => o.BoundsChanged(e));

            Children.Add(TileLayer);
        }

        private void CenterChange(AvaloniaPropertyChangedEventArgs e)
        {
            if (!Bounds.IsEmpty)
            {
                Transform.SetZoom(Center, ZoomLevel, Bounds);
            }
        }

        private void MapTileLayerChange(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.OldValue is MapTileLayer oldLayer)
            {
                Children.Remove(oldLayer);
            }

            if (e.NewValue is MapTileLayer newLayer)
            {
                Children.Insert(0, newLayer);
            }
        }

        private void BoundsChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is Rect bounds)
            {
                Transform.SetZoom(Center, ZoomLevel, bounds);
            }
        }
    }
}