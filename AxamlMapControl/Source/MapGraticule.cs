using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AxamlMapControl.Source.Proj;

namespace AxamlMapControl.Source
{
    public class MapGraticule : MapBaseElement
    {
        private const int MaximumCachedLabels = 512;
        private const double CachePadding = 256;
        private static readonly int[] LineDistances = { 1, 2, 5, 10, 15, 30, 60 };
        private readonly Dictionary<(double Coordinate, bool Latitude), FormattedText> _labels = new();
        private readonly List<(Point Start, Point End)> _lines = new();
        private readonly List<(Point Position, FormattedText Latitude, FormattedText Longitude)> _crossings = new();
        private ViewTransform? _cachedTransform;
        private Point _cachedCenter;
        private Size _cachedSize;
        private Rect _cachedArea;
        private double _cachedScale;
        private double _cachedDistance;
        private bool _cacheValid;

        public MapGraticule()
        {
            PropertyChanged += (_, e) =>
            {
                if (e.Property == FontFamilyProperty || e.Property == FontSizeProperty ||
                    e.Property == FontStyleProperty || e.Property == FontWeightProperty ||
                    e.Property == ForegroundProperty)
                {
                    _labels.Clear();
                    _cacheValid = false;
                    InvalidateVisual();
                }
            };
            DetachedFromVisualTree += (_, _) =>
            {
                _labels.Clear();
                _lines.Clear();
                _crossings.Clear();
                _cachedTransform = null;
                _cacheValid = false;
            };
        }

        protected override void OnViewChanged(int zoomLevel) => InvalidateVisual();

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            if (MapParent is not { } map || !(map.Bounds.Width > 0 && map.Bounds.Height > 0)) return;
            var transform = Transform;
            var size = map.Bounds.Size;
            var distance = CalculateLineDistance(150);
            var center = transform.ViewCenter;
            var delta = new Vector(center.X - _cachedCenter.X, center.Y - _cachedCenter.Y);
            if (!_cacheValid || !ReferenceEquals(transform, _cachedTransform) ||
                !_cachedScale.Equals(transform.Scale) || !_cachedDistance.Equals(distance) ||
                !_cachedSize.Equals(size) ||
                _cachedArea.X + delta.X > 0 || _cachedArea.Y + delta.Y > 0 ||
                _cachedArea.Right + delta.X < size.Width || _cachedArea.Bottom + delta.Y < size.Height)
            {
                RebuildCache(transform, size, distance);
                delta = new Vector();
            }
            var viewport = new Rect(size);
            var offset = StrokeThickness / 2;
            using (context.PushClip(viewport))
            {
                foreach (var line in _lines)
                {
                    var start = line.Start + delta;
                    var end = line.End + delta;
                    var margin = Math.Max(1, Math.Abs(StrokeThickness) * 2);
                    if (Math.Max(start.X, end.X) < -margin || Math.Min(start.X, end.X) > size.Width + margin ||
                        Math.Max(start.Y, end.Y) < -margin || Math.Min(start.Y, end.Y) > size.Height + margin) continue;
                    context.DrawLine(Pen, start, end);
                }
                foreach (var crossing in _crossings)
                {
                    var position = crossing.Position + delta;
                    DrawLabel(context, crossing.Latitude,
                        position - new Point(-2 - offset, FontSize + 4 + offset), viewport);
                    DrawLabel(context, crossing.Longitude,
                        position + new Point(2 + offset, offset), viewport);
                }
            }
        }

        private static void DrawLabel(DrawingContext context, FormattedText text, Point position, Rect viewport)
        {
            var bounds = new Rect(position, new Size(text.Width, text.Height));
            if (bounds.Intersects(viewport)) context.DrawText(text, position);
        }

        private void RebuildCache(ViewTransform transform, Size size, double distance)
        {
            _cacheValid = false;
            _lines.Clear();
            _crossings.Clear();
            _cachedArea = new Rect(-CachePadding, -CachePadding,
                size.Width + 2 * CachePadding, size.Height + 2 * CachePadding);
            transform.GetMapBoundingBox(_cachedArea)?.VisitingRoundByDistance(distance,
                (start, end) => _lines.Add((transform.LocationToView(start), transform.LocationToView(end))),
                location => _crossings.Add((transform.LocationToView(location),
                    GetLabel(location, true), GetLabel(location, false))));
            _cachedTransform = transform;
            _cachedCenter = transform.ViewCenter;
            _cachedSize = size;
            _cachedScale = transform.Scale;
            _cachedDistance = distance;
            _cacheValid = true;
        }

        private FormattedText GetLabel(Location location, bool latitude)
        {
            var key = (latitude ? location.Latitude : location.Longitude, latitude);
            if (_labels.TryGetValue(key, out var label)) return label;
            if (_labels.Count >= MaximumCachedLabels) _labels.Clear();
            var text = latitude ? location.GetLatitudeDms().ToString() : location.GetLongitudeDms().ToString();
            label = new FormattedText(text, CultureInfo.InvariantCulture,
                Avalonia.Media.FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle, FontWeight), FontSize, Foreground);
            _labels.Add(key, label);
            return label;
        }

        private double CalculateLineDistance(double minLineDistance)
        {
            var pixelPerDegree = MapProjection.Wgs84MetersPerDegree / Transform.Scale;
            var minDistance = minLineDistance / pixelPerDegree;
            double scale = minDistance switch
            {
                var x when x < (1 / 60.0) => 3600,
                var x when x < 1 => 60,
                _ => 1
            };
            var distance = 60;
            foreach (var candidate in LineDistances)
            {
                if (candidate <= minDistance * scale) continue;
                distance = candidate;
                break;
            }
            return distance / scale;
        }
    }
}
