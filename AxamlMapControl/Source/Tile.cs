using System.ComponentModel.DataAnnotations.Schema;

namespace AxamlMapControl.Source
{
    /// <summary>
    /// Map tile.
    /// </summary>
    public class Tile
    {
        public int Zoom { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public byte[]? ImageData { get; set; }

        /// <summary>
        /// Normalize X. Use for render in MapTileLayer.
        /// </summary>
        [NotMapped]
        public int XIndex { get; set; }

        public Tile(int zoom, int x, int y, byte[] data)
        {
            Zoom = zoom;
            X = x;
            Y = y;
            ImageData = data;
        }

        public Tile(int zoom, int x, int y) : this(zoom, x, y, default!) { }
        public Tile() { }

        public static bool operator ==(Tile left, Tile right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Tile left, Tile right)
        {
            return !(left == right);
        }

        public bool Equals(Tile other)
        {
            return Zoom == other.Zoom &&
                   X == other.X &&
                   Y == other.Y;
        }

        public override bool Equals(object? obj)
        {
            return (obj is Tile other) && Equals(other);
        }

        public override int GetHashCode()
        {
            int hashCode = 2105055860;

            hashCode *= -1521134295 + Zoom.GetHashCode();
            hashCode *= -1521134295 + X.GetHashCode();
            hashCode *= -1521134295 + Y.GetHashCode();

            return hashCode;
        }
    }
}