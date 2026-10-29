using System.Linq;
using Avalonia;
using Avalonia.Media;
using AxamlMapControl.Source.Proj;

namespace AxamlMapControl.Source
{
    public class MapGraticule : MapBaseElement
    {
        protected override void OnViewChanged(int zoomLevel) => InvalidateVisual();

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            if (MapParent is not { } map || map.Bounds.IsEmpty) return;
            var lineDistance = CalculateLineDistance(150);
            var bounds = new Rect(0, 0, map.Bounds.Width, map.Bounds.Height);
            Transform.GetMapBoundingBox(bounds)?.VisitingRoundByDistance(lineDistance,
                (loc1, loc2) => context.DrawLine(Pen, Transform.LocationToView(loc1), Transform.LocationToView(loc2)),
                location =>
                {
                    var viewPoint = Transform.LocationToView(location);
                    var offset = StrokeThickness / 2;
                    context.DrawText(Foreground, viewPoint - new Point(-2 - offset, FontSize + 4 + offset),
                        SetTextFormat(location.GetLatitudeDms().ToString()));
                    context.DrawText(Foreground, viewPoint + new Point(2 + offset, 0 + offset),
                        SetTextFormat(location.GetLongitudeDms().ToString()));
                });
        }

        private FormattedText SetTextFormat(string text) => new FormattedText(
            text, new Typeface(FontFamily, FontStyle, FontWeight), FontSize,
            TextAlignment.Left, TextWrapping.NoWrap, new Size(150, 30));

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
            var distance = new int?[] { 1, 2, 5, 10, 15, 30, 60 }
                .FirstOrDefault(value => value > minDistance * scale) ?? 60;
            return distance / scale;
        }
    }
}
