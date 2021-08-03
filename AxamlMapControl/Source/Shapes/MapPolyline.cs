using System;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;
using AxamlMapControl.Source.Proj;

namespace AxamlMapControl.Source.Shapes
{
    /// <summary>
    /// Map shape of polyline.
    /// </summary>
    public class MapPolyline : MapPath
    {
        public static readonly StyledProperty<Locations> LocationsProperty
            = AvaloniaProperty.Register<MapPolyline, Locations>(nameof(Locations));

        public Locations Locations
        {
            get => GetValue(LocationsProperty);
            set => SetValue(LocationsProperty, value);
        }

        public MapPolyline()
        {
            var geometry = new PathGeometry();
            geometry.Figures.ResetBehavior = ResetBehavior.Remove;
            Data = geometry;

            LocationsProperty.Changed.AddClassHandler<MapPolyline>((o, e) => o.LocationsChanged(e));
        }

        private void LocationsChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (Locations.Count <= 1)
            {
                throw new Exception("Locations count less 2.");
            }

            MapLocationLayer.SetLocation(this, GetTopLeftLocation(Locations));

            if (MapParent != null)
            {
                Update();
            }
        }

        protected override void Update()
        {
            Update(false, false);
        }

        /// <summary>
        /// Update
        /// </summary>
        /// <param name="isClosed">Closed</param>
        /// <param name="isFilled">Filled</param>
        protected void Update(bool isClosed, bool isFilled)
        {
            if (Data is PathGeometry data)
            {
                var points = Locations.Select(p => MapParent.Transform.LocationToCartesian(p));

                data.Figures.Clear();
                data.Figures.Add(CreateFigure(points.Cast<Point>(), isClosed, isFilled));

                // TODO: Transform method
                // var scale = MapParent.Transform.GetScale(location);            
                // Data.Transform = new MatrixTransform(new Matrix(1 / scale.X, 0, 0, 1 / -scale.Y, 1 / -scale.X * p.Value.X, 1 / scale.Y * p.Value.Y));

                var p = MapParent.Transform.LocationToCartesian(MapLocationLayer.GetLocation(this));
                var scale = MapParent.Transform.Scale;
                Data.Transform = new MatrixTransform(new Matrix(1 / scale, 0, 0, 1 / -scale, 1 / -scale * p.X, 1 / scale * p.Y));
            }
        }
    }
}