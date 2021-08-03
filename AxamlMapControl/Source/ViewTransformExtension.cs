using Avalonia;
using AxamlMapControl.Source.Proj;

namespace AxamlMapControl.Source
{
    /// <summary>
    /// Extension of ViewTransform.
    /// </summary>
    public static class ViewTransformExtension
    {
        /// <summary>
        /// Get scale in the current location.
        /// </summary>
        /// <param name="mapTransform">ViewTransform</param>
        /// <param name="location">Location</param>
        /// <returns>Scale</returns>
        public static Vector GetScale(this ViewTransform mapTransform, Location location)
        {
            return mapTransform.Scale * mapTransform.Projection.GetRelativeScale(location);
        }

        /// <summary>
        /// Get location from cartesian coordinate.
        /// </summary>
        /// <param name="mapTransform">ViewTransform</param>
        /// <param name="point">Cartesian coordinate</param>
        /// <returns>Location</returns>
        public static Location CartesianToLocation(this ViewTransform mapTransform, Point point)
        {
            if (mapTransform.Projection is null)
            {
                throw new System.NullReferenceException("Projection is null.");
            }

            return mapTransform.Projection.ToLocation(point);
        }

        /// <summary>
        /// Get cartesian coordinate from location.
        /// </summary>
        /// <param name="mapTransform">ViewTransform</param>
        /// <param name="loc">Location</param>
        /// <returns>Cartesian coordinate</returns>
        public static Point LocationToCartesian(this ViewTransform mapTransform, Location loc)
        {
            if (mapTransform.Projection is null)
            {
                throw new System.NullReferenceException("Projection is null.");
            }

            return mapTransform.Projection.ToCartesian(loc);
        }

        /// <summary>
        /// Get display coordinate from location.
        /// </summary>
        /// <param name="mapTransform">ViewTransform</param>
        /// <param name="location">Location</param>
        /// <returns>Coordinate</returns>
        public static Point LocationToView(this ViewTransform mapTransform, Location location)
        {
            return mapTransform.CartesianToView(mapTransform.Projection.ToCartesian(location));
        }

        /// <summary>
        /// Get location from display coordinate.
        /// </summary>
        /// <param name="mapTransform">ViewTransform</param>
        /// <param name="point">Coordinate</param>
        /// <returns>Location</returns>
        public static Location ViewToLocation(this ViewTransform mapTransform, Point point)
        {
            return mapTransform.Projection.ToLocation(mapTransform.ViewToCartesian(point));
        }

        /// <summary>
        /// Get map bounding from the rect.
        /// </summary>
        /// <param name="mapTransform">ViewTransform</param>
        /// <param name="viewRect">Rect</param>
        /// <returns>Map bounding</returns>
        public static MapBoundingBox GetMapBoundingBox(this ViewTransform mapTransform, Rect viewRect)
        {
            var nw = mapTransform.ViewToLocation(viewRect.TopLeft);
            var se = mapTransform.ViewToLocation(viewRect.BottomRight);

            return new MapBoundingBox(nw.Longitude, nw.Latitude, se.Longitude, se.Latitude);
        }
    }
}