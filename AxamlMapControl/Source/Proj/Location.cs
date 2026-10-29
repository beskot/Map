using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia;

namespace AxamlMapControl.Source.Proj
{
    /// <summary>Location (latitude, longitude).</summary>
    public class Location : IEquatable<Location>
    {
        public const double MinLatitude = -90;
        public const double MaxLatitude = 90;
        public const double MinLongitude = -180;
        public const double MaxLongitude = 180;
        private double _latitude;
        private double _longitude;
        public double Latitude => _latitude;
        public double Longitude => _longitude;

        public Location(double latitide, double longitude) =>
            (_latitude, _longitude) = (latitide, longitude);
        public Location() : this(0, 0) { }

        public void Deconstructor(out double latitude, out double longitude) =>
            (latitude, longitude) = (_latitude, _longitude);

        public double NormalizeLatitude(double latitude) =>
            Math.Min(Math.Max(latitude, MinLatitude), MaxLatitude);

        public static double NormalizeLongitude(double longitude)
        {
            if (longitude < MinLongitude) longitude = ((longitude + 180) % 360) + 180;
            else if (longitude > MaxLongitude) longitude = ((longitude - 180) % 360) - 180;
            return longitude;
        }

        public Location NormalizeLongitude()
        {
            _longitude = NormalizeLongitude(_longitude);
            return this;
        }

        public static Location Parse(string s) => CoordinateTextParser.ParseLocation(s);

        public static Location operator +(Location loc1, Location loc2) =>
            new(loc1._latitude + loc2._latitude, loc1._longitude + loc2._longitude);
        public static Location operator -(Location loc1, Location loc2) =>
            new(loc1._latitude - loc2._latitude, loc1._longitude - loc2._longitude);
        public static Location operator -(Location loc) => new(-loc._latitude, -loc._longitude);
        public static bool operator ==(Location left, Location right) => left.Equals(right);
        public static bool operator !=(Location left, Location right) => !(left == right);
        public Location WithLatitude(double latitude) => new(latitude, _longitude);
        public Location WithLongitude(double longitude) => new(_latitude, longitude);

        public bool Equals(Location? other) => other is { } &&
            Math.Abs(other._latitude - _latitude) < 1e-9 &&
            Math.Abs(other._longitude - _longitude) < 1e-9;

        public override bool Equals(object? obj) => obj is Location other && Equals(other);

        public override int GetHashCode()
        {
            int hashCode = 2105055860;
            hashCode = hashCode * -1521134295 + _latitude.GetHashCode();
            hashCode = hashCode * -1521134295 + _longitude.GetHashCode();
            hashCode = hashCode * -1521134295 + Latitude.GetHashCode();
            hashCode = hashCode * -1521134295 + Longitude.GetHashCode();
            return hashCode;
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "{0}, {1}", _latitude, _longitude);
    }

    public class CartesianComparer : IEqualityComparer<Point>
    {
        public bool Equals(Point x, Point y) =>
            Math.Abs(x.X - y.X) < 1e-9 && Math.Abs(x.Y - y.Y) < 1e-9;
        public int GetHashCode([DisallowNull] Point obj) => obj.GetHashCode();
    }
}
