
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.VisualTree;
using AxamlMapControl.Source.Proj;

namespace AxamlMapControl.Source.Shapes
{
    /// <summary>
    /// Map shape base.
    /// </summary>
    public class MapPath : Path, IMapElement
    {
        public Map MapParent { get; private set; } = default!;

        static MapPath()
        {
            DataProperty.AddOwner<MapPath>();
        }

        public MapPath()
        {
            AttachedToVisualTree += (o, e) =>
            {
                MapParent = this.FindAncestorOfType<Map>();
            };
            ClipToBounds = true;
        }

        protected override Geometry CreateDefiningGeometry()
        {
            return Data;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (MapParent != null)
            {
                Update();
            }

            return finalSize;
        }

        /// <summary>
        /// Update of the path.
        /// </summary>
        protected virtual void Update()
        {
            var location = MapLocationLayer.GetLocation(this);
            var scale = MapParent.Transform.GetScale(location);
            var matrix = new Matrix(1 / scale.X, 0, 0, 1 / -scale.Y, DesiredSize.Width / 2, DesiredSize.Height / 2);

            Data.Transform = new MatrixTransform(matrix);
        }

        /// <summary>
        /// Get TopLeft location of the path.
        /// </summary>
        /// <param name="locations">Locations</param>
        /// <returns>Location</returns>
        protected Location GetTopLeftLocation(IEnumerable<Location> locations)
        {
            var lat = locations.Select(p => p.Latitude).Max();
            var lon = locations.Select(p => p.Longitude).Min();

            return new Location(lat, lon);
        }

        /// <summary>
        /// Create figure.
        /// </summary>
        /// <param name="points">Points</param>
        /// <param name="isClosed">IsClosed</param>
        /// <param name="isFilled">IsFilled</param>
        /// <returns>Path figure</returns>
        protected PathFigure CreateFigure(IEnumerable<Point> points, bool isClosed, bool isFilled)
        {
            PathFigure figure = new PathFigure
            {
                StartPoint = points.First(),
                IsClosed = isClosed,
                IsFilled = isFilled,
            };

            figure.Segments?.AddRange(points.Skip(1).Select(p => new LineSegment { Point = p }));

            return figure;
        }
    }
}