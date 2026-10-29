using System;
using Avalonia;

namespace AxamlMapControl.Source.Proj
{
    /// <summary>Spherical Mercator projection for a square raster-tile world.</summary>
    public class MapProjection : IMapProjection
    {
        public const double Wgs84EquatorialRadius = 6378137;
        public const double Wgs84MetersPerDegree = Wgs84EquatorialRadius * Math.PI / 180;
        public const double OriginShift = Math.PI * Wgs84EquatorialRadius;

        /// <summary>Latitude at the square world's vertical edge: atan(sinh(pi)).</summary>
        public static readonly double MaxLatitude = Math.Atan(Math.Sinh(Math.PI)) * 180d / Math.PI;
        public static readonly double MinLatitude = -MaxLatitude;

        private static void ValidateLocation(Location location)
        {
            if (location is null) throw new ArgumentNullException(nameof(location));
            if (!double.IsFinite(location.Latitude) || !double.IsFinite(location.Longitude))
                throw new ArgumentOutOfRangeException(nameof(location),
                    "Latitude and longitude must be finite.");
        }

        public Vector GetRelativeScale(Location location)
        {
            ValidateLocation(location);
            var latitude = Math.Clamp(location.Latitude, MinLatitude, MaxLatitude);
            var scale = 1d / Math.Cos(latitude * Math.PI / 180d);
            return new Vector(scale, scale);
        }

        public Location ToLocation(Point point)
        {
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
                throw new ArgumentOutOfRangeException(nameof(point),
                    "Projected coordinates must be finite.");
            var y = Math.Clamp(point.Y, -OriginShift, OriginShift);
            var latitude = Math.Atan(Math.Sinh(y / Wgs84EquatorialRadius)) * 180d / Math.PI;
            // Preserve unwrapped longitude for horizontal world repetition.
            return new Location(latitude, point.X / Wgs84MetersPerDegree);
        }

        public Point ToCartesian(Location location)
        {
            ValidateLocation(location);
            var latitude = Math.Clamp(location.Latitude, MinLatitude, MaxLatitude);
            var x = Wgs84MetersPerDegree * location.Longitude;
            if (!double.IsFinite(x))
                throw new ArgumentOutOfRangeException(nameof(location),
                    "Longitude exceeds the supported projected coordinate range.");
            var y = Wgs84EquatorialRadius * Math.Asinh(Math.Tan(latitude * Math.PI / 180d));
            return new Point(x, Math.Clamp(y, -OriginShift, OriginShift));
        }
    }
}
