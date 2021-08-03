using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;
using AxamlMapControl.Source.Proj;

namespace AxamlMapControl.Source.Shapes
{
    /// <summary>
    /// Map shape of line.
    /// </summary>
    public class MapLine : MapPath
    {
        public static readonly StyledProperty<Location> StartPointProperty
            = AvaloniaProperty.Register<MapLine, Location>(nameof(StartPoint), new(), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

        public static readonly StyledProperty<Location> EndPointProperty
            = AvaloniaProperty.Register<MapLine, Location>(nameof(EndPoint), new(), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

        public Location StartPoint
        {
            get => GetValue(StartPointProperty);
            set => SetValue(StartPointProperty, value);
        }

        public Location EndPoint
        {
            get => GetValue(EndPointProperty);
            set => SetValue(EndPointProperty, value);
        }

        private Location[] _locations = new Location[2];

        public MapLine()
        {
            var geometry = new PathGeometry();
            geometry.Figures.ResetBehavior = ResetBehavior.Remove;
            Data = geometry;

            StartPointProperty.Changed.AddClassHandler<MapLine>((_, e) => LocationsChanged(e));
            EndPointProperty.Changed.AddClassHandler<MapLine>((_, e) => LocationsChanged(e));
        }

        private void LocationsChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is Location value)
            {
                if (e.Property == StartPointProperty)
                {
                    _locations[0] = value;
                }
                else if (e.Property == EndPointProperty)
                {
                    _locations[1] = value;
                }

                if (_locations[0] is { } &&
                    _locations[1] is { })
                {
                    MapLocationLayer.SetLocation(this, GetTopLeftLocation(_locations));
                }
            }
        }

        protected override void Update()
        {
            if (Data is PathGeometry data)
            {
                var points = _locations.Select(p => MapParent.Transform.LocationToCartesian(p));
                var pointTranslated = MapParent.Transform.LocationToCartesian(MapLocationLayer.GetLocation(this));
                var scale = MapParent.Transform.Scale;

                data.Figures.Clear();
                data.Figures.Add(CreateFigure(points.Cast<Point>(), false, false));

                Data.Transform = new MatrixTransform(
                    new Matrix(1 / scale, 0, 0, 1 / -scale, 1 / -scale * pointTranslated.X, 1 / scale * pointTranslated.Y));
            }
        }
    }
}