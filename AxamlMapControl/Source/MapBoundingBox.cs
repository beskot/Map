using System;
using System.Globalization;
using AxamlMapControl.Source.Proj;

namespace AxamlMapControl.Source
{
    /// <summary>
    /// Map bounding box.
    /// </summary>
    public class MapBoundingBox
    {
        public double West { get; private set; }
        public double North { get; private set; }
        public double East { get; private set; }
        public double South { get; private set; }

        public static MapBoundingBox Empty => new(0, 0, 0, 0);

        public MapBoundingBox(double west, double north, double east, double south)
        {
            West = west;
            North = north;
            East = east;
            South = south;
        }

        /// <summary>
        /// Visiting bounding box by distance.
        /// </summary>
        /// <param name="distance">Distance</param>
        /// <param name="LatitudeAct">Action of visiting latitude by distanse</param>
        /// <param name="LongitudeAct">Action of visiting longitude by distanse</param>
        /// <param name="LocationAct">Action of visiting (latitude, longitude) by distanse</param>
        private void VisitingRoundByDistance(double distance, Action<Location, Location> LatitudeAct, Action<Location, Location> LongitudeAct, Action<Location> LocationAct)
        {
            var latBegin = Math.Ceiling(South / distance) * distance;
            var lonBegin = Math.Ceiling(West / distance) * distance;

            for (var lat = latBegin - distance; lat <= North + distance; lat += distance)
            {
                LatitudeAct?.Invoke(new(lat, West), new(lat, East));
                for (var lon = lonBegin - distance; lon <= East + distance; lon += distance)
                {
                    LocationAct?.Invoke(new Location(lat, lon));
                }
            }

            for (var lon = lonBegin - distance; lon <= East + distance; lon += distance)
            {
                LongitudeAct?.Invoke(new(South, lon), new(North, lon));
            }
        }

        /// <summary>
        /// Visiting bounding box by distance.
        /// </summary>
        /// <param name="distance">Distance</param>
        /// <param name="AxisAct">Action of visiting of latitude and longitude by distance</param>
        /// <param name="CrossAxisAct">Action of visiting (latitude, longitude) by distanse</param>
        public void VisitingRoundByDistance(double distance, Action<Location, Location> AxisAct, Action<Location> CrossAxisAct)
        {
            VisitingRoundByDistance(distance, AxisAct, AxisAct, CrossAxisAct);
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}, {3}", West, North, East, South);
        }
    }
}