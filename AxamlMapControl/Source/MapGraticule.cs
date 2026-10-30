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
        private static readonly int[] LineDistances = { 1, 2, 5, 10, 15, 30, 60 };
        private readonly Dictionary<(double Coordinate, bool Latitude), FormattedText> _labels = new();

        public MapGraticule()
        {
            PropertyChanged += (_, e) =>
            {
                if (e.Property == FontFamilyProperty || e.Property == FontSizeProperty ||
                    e.Property == FontStyleProperty || e.Property == FontWeightProperty ||
                    e.Property == ForegroundProperty)
                {
                    _labels.Clear();
                    InvalidateVisual();
                }
            };
            DetachedFromVisualTree += (_, _) => _labels.Clear();
        }

        protected override void OnViewChanged(int zoomLevel) => InvalidateVisual();

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            if (MapParent is not { } map) return;
            if (!(map.Bounds.Width > 0 && map.Bounds.Height > 0)) return;
            var lineDistance = CalculateLineDistance(150);
            var bounds = new Rect(0, 0, map.Bounds.Width, map.Bounds.Height);
            Transform.GetMapBoundingBox(bounds)?.VisitingRoundByDistance(lineDistance,
                (loc1, loc2) => context.DrawLine(Pen, Transform.LocationToView(loc1), Transform.LocationToView(loc2)),
                location =>
                {
                    var viewPoint = Transform.LocationToView(location);
                    var offset = StrokeThickness / 2;
                    context.DrawText(GetLabel(location, true),
                        viewPoint - new Point(-2 - offset, FontSize + 4 + offset));
                    context.DrawText(GetLabel(location, false),
                        viewPoint + new Point(2 + offset, 0 + offset));
                });
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
