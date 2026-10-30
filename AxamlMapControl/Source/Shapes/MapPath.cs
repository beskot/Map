using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.VisualTree;
using AxamlMapControl.Source.Proj;

namespace AxamlMapControl.Source.Shapes
{
    public class MapPath : Path, IMapElement
    {
        public Map MapParent { get; private set; } = default!;
        private bool _updating;
        private bool _geometryDirty = true;
        private bool _geographicGeometry;
        private double _geometryScale = double.NaN;

        static MapPath()
        {
            DataProperty.Changed.AddClassHandler<MapPath>((path, _) =>
            {
                if (!path._updating)
                {
                    path._geometryDirty = true;
                    path._geographicGeometry = false;
                }
                path.InvalidateMeasure();
            });
            MapLocationLayer.LocationProperty.Changed.AddClassHandler<MapPath>(
                (path, _) => path.RefreshGeometry());
        }

        public MapPath()
        {
            AttachedToVisualTree += (_, _) => SetMapParent(this.FindAncestorOfType<Map>());
            DetachedFromVisualTree += (_, _) => SetMapParent(null);
            ClipToBounds = true;
        }

        internal void SetMapParent(Map? map)
        {
            if (ReferenceEquals(MapParent, map)) return;
            var previous = MapParent;
            if (previous is { }) previous.Transform.ViewBoundsChanged -= ViewChanged;
            MapParent = map!;
            OnMapParentChanged(previous, map);
            if (map is { }) map.Transform.ViewBoundsChanged += ViewChanged;
            RefreshGeometry();
        }

        protected virtual void OnMapParentChanged(Map? previous, Map? current) { }

        private void ViewChanged(object? sender, MapEventArgs e)
        {
            if (MapParent is not { } map || !ReferenceEquals(sender, map.Transform)) return;
            // MapLocationLayer handles position changes; local geometry depends on scale, not pan.
            if (_geometryDirty || !_geometryScale.Equals(map.Transform.Scale)) RefreshGeometry();
        }

        internal void RefreshGeometry()
        {
            if (_updating) return;
            _geometryDirty = true;
            UpdateGeometry();
            InvalidateMeasure();
            InvalidateVisual();
        }

        private void UpdateGeometry()
        {
            if (_updating || !_geometryDirty) return;
            _updating = true;
            try
            {
                Update();
                _geometryDirty = false;
                _geometryScale = MapParent is { } map ? map.Transform.Scale : double.NaN;
            }
            finally { _updating = false; }
        }

        protected override Geometry CreateDefiningGeometry() => Data;

        protected override Size ArrangeOverride(Size finalSize)
        {
            UpdateGeometry();
            // Non-geographic paths (e.g. an ellipse in metres) use the measured size as their anchor.
            if (!_geographicGeometry && !_updating)
            {
                _updating = true;
                try { Update(); }
                finally { _updating = false; }
            }
            return finalSize;
        }

        protected virtual void Update()
        {
            if (MapParent is not { } map || Data is not { } data ||
                MapLocationLayer.GetLocation(this) is not { } location) return;
            var scale = map.Transform.GetScale(location);
            var matrix = new Matrix(
                1 / scale.X, 0, 0, -1 / scale.Y, DesiredSize.Width / 2, DesiredSize.Height / 2);
            if (data.Transform is MatrixTransform transform && transform.Matrix.Equals(matrix)) return;
            data.Transform = new MatrixTransform(matrix);
        }

        protected void UpdateGeographicGeometry(IEnumerable<Location>? locations,
            int minimumPoints, bool isClosed, bool isFilled)
        {
            _geographicGeometry = true;
            var values = locations?.ToArray() ?? Array.Empty<Location>();
            if (Data is not PathGeometry data)
            {
                data = new PathGeometry();
                Data = data;
            }
            // Geometry observers do not support collection Reset notifications.
            while (data.Figures.Count > 0)
            {
                data.Figures.RemoveAt(data.Figures.Count - 1);
            }
            if (values.Length < minimumPoints || values.Any(location => location is null ||
                !double.IsFinite(location.Latitude) || !double.IsFinite(location.Longitude)))
            {
                MapLocationLayer.SetLocation(this, null);
                return;
            }

            var origin = GetTopLeftLocation(values);
            var currentOrigin = MapLocationLayer.GetLocation(this);
            if (currentOrigin is null || !currentOrigin.Equals(origin))
                MapLocationLayer.SetLocation(this, origin);
            if (MapParent is not { } map) return;
            var projectedOrigin = map.Transform.LocationToCartesian(origin);
            var scale = map.Transform.Scale;
            var points = values.Select(location =>
            {
                var point = map.Transform.LocationToCartesian(location);
                return new Point((point.X - projectedOrigin.X) / scale,
                    (projectedOrigin.Y - point.Y) / scale);
            }).ToArray();
            data.Transform = null;
            data.Figures.Add(CreateFigure(points, isClosed, isFilled));
        }

        protected Location GetTopLeftLocation(IEnumerable<Location> locations)
        {
            var values = locations?.ToArray() ?? Array.Empty<Location>();
            if (values.Length == 0) return new Location();
            return new Location(values.Max(location => location.Latitude),
                values.Min(location => location.Longitude));
        }

        protected PathFigure CreateFigure(IEnumerable<Point> points, bool isClosed, bool isFilled)
        {
            var values = points?.ToArray() ?? Array.Empty<Point>();
            var figure = new PathFigure
            {
                IsClosed = isClosed,
                IsFilled = isFilled
            };
            if (values.Length == 0) return figure;
            figure.StartPoint = values[0];
            figure.Segments?.AddRange(values.Skip(1).Select(point => new LineSegment { Point = point }));
            return figure;
        }
    }
}
