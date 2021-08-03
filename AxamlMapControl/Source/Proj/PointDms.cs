using System;

namespace AxamlMapControl.Source.Proj
{
    public enum PointDmsType
    {
        Latitude,
        Longitude
    }

    /// <summary>
    /// Degrees/Minutes/Seconds format of double value (latitude or longitude)
    /// </summary>
    public class PointDms
    {
        public int Degrees { get; set; }
        public int Minutes { get; set; }
        public int Seconds { get; set; }
        public PointDmsType Type { get; set; }

        public double Value => (Degrees + Minutes / 60.0 + Seconds / 3600.0);

        private PointDms(int degree, int minute, int second, PointDmsType type)
        {
            Degrees = degree;
            Minutes = minute;
            Seconds = second;
            Type = type;
        }

        /// <summary>
        /// Create Lat or Lon dms format.
        /// </summary>
        /// <param name="value">Value</param>
        /// <param name="type">Point type</param>
        /// <returns>Format dms point</returns>
        public static PointDms CreatePointDms(double value, PointDmsType type)
        {
            var seconds = (int)Math.Round(value * 3600d);
            var degrees = seconds / 3600.0;
            var minutes = (seconds / 60.0) % 60;

            return new((int)degrees, (int)minutes, seconds % 60, type);
        }

        public override string ToString()
        {
            return string.Format("{0}°{1}'{2}''{3}",
                Math.Abs(Degrees),
                Math.Abs(Minutes),
                Math.Abs(Seconds),
                Type == PointDmsType.Latitude
                    ? Degrees < 0 ? "S" : "N"
                    : Degrees < 0 ? "W" : "E");
        }
    }
}