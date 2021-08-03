using System;
using Avalonia;

namespace AxamlMapControl.Source.Proj
{
    /// <summary>
    /// Spherical Mercator Projection, EPSG:3857.
    /// </summary>
    public class MapProjection : IMapProjection
    {
        /// <summary>
        /// Radius of Earth (m)
        /// </summary>
        public const double Wgs84EquatorialRadius = 6378137;

        /// <summary>
        /// Mert in degree
        /// </summary>
        public const double Wgs84MetersPerDegree = Wgs84EquatorialRadius * Math.PI / 180;

        /// <summary>
        /// Offset from origin
        /// </summary>
        public const double OriginShift = Math.PI * MapProjection.Wgs84EquatorialRadius;

        /// <summary>
        /// Get relative scale.
        /// </summary>
        /// <param name="location">Location</param>
        /// <returns>Scale</returns>
        public Vector GetRelativeScale(Location location)
        {
            var k = 1d / Math.Cos(location.Latitude * Math.PI / 180d);

            return new Vector(k, k);
        }

        /// <summary>
        /// Get location from cartesian coordinate.
        /// </summary>
        /// <param name="point">Cartesian coordinate</param>
        /// <returns>Location</returns>
        public Location ToLocation(Point point)
        {
            return new Location(
                YToLatitude(point.Y / Wgs84MetersPerDegree),
                point.X / Wgs84MetersPerDegree);
        }

        /// <summary>
        /// Get cartesian coordinate from location.
        /// </summary>
        /// <param name="location">Location</param>
        /// <returns>Cartesian coordinate</returns>
        public Point ToCartesian(Location location)
        {
           return new Point(
                Wgs84MetersPerDegree * location.Longitude,
                Wgs84MetersPerDegree * LatitudeToY(location.Latitude));
        }

        private double LatitudeToY(double latitude)
        {
            if (latitude <= Location.MinLatitude)
            {
                return double.NegativeInfinity;
            }

            if (latitude >= Location.MaxLatitude)
            {
                return double.PositiveInfinity;
            }

            return Math.Log(Math.Tan((latitude + 90d) * Math.PI / 360d)) * 180d / Math.PI;
        }

        private double YToLatitude(double y)
        {
            return 90d - Math.Atan(Math.Exp(-y * Math.PI / 180d)) * 360d / Math.PI;
        }
    }
}