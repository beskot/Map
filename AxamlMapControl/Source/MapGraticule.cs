using System.Linq;
using Avalonia;
using Avalonia.Media;
using AxamlMapControl.Source.Proj;

namespace AxamlMapControl.Source
{
    /// <summary>
    /// Map graticule element.
    /// </summary>
    public class MapGraticule : MapBaseElement
    {
        protected override void OnViewChanged(int zoomLevel)
        {
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var lineDistance = CalculateLineDistance(150);

            Transform.GetMapBoundingBox(MapParent.Bounds)?.VisitingRoundByDistance(lineDistance,
                (loc1, loc2) => context.DrawLine(Pen, Transform.LocationToView(loc1), Transform.LocationToView(loc2)),
                (location) =>
                {
                    var viewPoint = Transform.LocationToView(location);
                    var offset = StrokeThickness / 2;

                    context.DrawText(
                        Foreground,
                        viewPoint - new Point(-2 - offset, FontSize + 4 + offset),
                        SetTextFormat(location.GetLatitudeDms().ToString()));

                    context.DrawText(
                        Foreground,
                        viewPoint + new Point(2 + offset, 0 + offset),
                        SetTextFormat(location.GetLongitudeDms().ToString()));
                });
        }

        /// <summary>
        /// Set text format.
        /// </summary>
        /// <param name="text">Text</param>
        /// <returns>Format text</returns>
        private FormattedText SetTextFormat(string text)
        {
            return new FormattedText(
                text,
                new Typeface(FontFamily, FontStyle, FontWeight),
                FontSize,
                TextAlignment.Left,
                TextWrapping.NoWrap,
                new Size(150, 30));
        }

        /// <summary>
        /// Calculate line distance.
        /// </summary>
        /// <param name="minLineDistance">Min value of distance</param>
        /// <returns>Distance</returns>
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
                .FirstOrDefault(p => p > (minDistance * scale)) ?? 60;

            return distance / scale;
        }
    }
}