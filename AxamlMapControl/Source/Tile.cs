using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace AxamlMapControl.Source
{
    /// <summary>Map tile identified by Zoom, X and Y.</summary>
    /// <remarks>Do not change coordinates while the tile is a dictionary or hash-set key.</remarks>
    public class Tile : IEquatable<Tile>
    {
        public int Zoom { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public byte[]? ImageData { get; set; }

        /// <summary>Unwrapped horizontal position used by the tile layer.</summary>
        [NotMapped]
        public int XIndex { get; set; }

        public Tile(int zoom, int x, int y, byte[]? data)
        {
            Zoom = zoom;
            X = x;
            Y = y;
            ImageData = data;
        }

        public Tile(int zoom, int x, int y) : this(zoom, x, y, null) { }
        public Tile() { }

        public bool Equals(Tile? other) => other is { } &&
            Zoom == other.Zoom && X == other.X && Y == other.Y;

        public override bool Equals(object? obj) => obj is Tile other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Zoom, X, Y);

        public static bool operator ==(Tile? left, Tile? right) =>
            ReferenceEquals(left, right) || (left is { } && left.Equals(right));

        public static bool operator !=(Tile? left, Tile? right) => !(left == right);
    }
}
