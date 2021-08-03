using System;
using Avalonia;

namespace AxamlMapControl.Source
{
    /// <summary>
    /// Tile matrix.
    /// </summary>
    internal sealed class MapTileMatrix
    {
        public int Zoom { get; private set; }
        public int Xmin { get; private set; }
        public int Ymin { get; private set; }
        public int Xmax { get; private set; }
        public int Ymax { get; private set; }
        public int NumTiles => 1 << Zoom;
        public static MapTileMatrix Empty => new();

        public int XLength => Xmax - Xmin;
        public int YLength => Ymax - Ymin;
        public int Length => XLength * YLength;

        public Point Origin => new Point(Xmin * MapTileLayer.TileSize, Ymin * MapTileLayer.TileSize);

        public bool IsEmpty
            => Zoom == -1 &&
                Xmin == 0 &&
                Ymin == 0 &&
                Xmax == 0 &&
                Ymax == 0;

        public MapTileMatrix(int zoom, int xmin, int ymin, int xmax, int ymax)
        {
            this.Zoom = zoom;
            this.Xmin = xmin;
            this.Ymin = ymin;
            this.Xmax = xmax;
            this.Ymax = ymax;
        }

        private MapTileMatrix() : this(-1, 0, 0, 0, 0)
        {

        }

        /// <summary>
        /// Create MapTileMatrix.
        /// </summary>
        /// <param name="zoom">Zoom</param>
        /// <param name="bounds">View bounds</param>
        /// <param name="tileWidth">Width of tile</param>
        /// <param name="tileHeight">Height of tile</param>
        /// <returns>MapTileMatrix</returns>
        public static MapTileMatrix Create(int zoom, Rect bounds, double tileWidth, double tileHeight)
        {
            var x = bounds.TopLeft.X;
            var y = bounds.TopLeft.Y;

            var xmin = (int)Math.Floor(x / tileWidth);
            var ymin = (int)Math.Floor(y / tileHeight);
            var xmax = (int)Math.Floor((x + bounds.Width) / tileWidth) + 1;
            var ymax = (int)Math.Floor((y + bounds.Height) / tileHeight) + 1;

            // xmin = Math.Max(xmin, 0);
            // xmax = Math.Min((1 << zoom), xmax);

            ymin = Math.Max(ymin, 0);
            ymax = Math.Min((1 << zoom), ymax);

            return new MapTileMatrix(zoom, xmin, ymin, xmax, ymax);
        }

        public static MapTileMatrix Create(int zoom, Rect bounds, double tileSize)
        {
            return Create(zoom, bounds, tileSize, tileSize);
        }

        /// <summary>
        /// Traverse cells of the matrix.
        /// </summary>
        /// <param name="visitAct">Action with params (int Zoom, int X, int Y)</param>
        public void TraverseСells(Action<int, int, int> visitAct)
        {
            if (!IsEmpty)
            {
                for (var y = Ymin; y < Ymax; y++)
                {
                    for (var x = Xmin; x < Xmax; x++)
                    {
                        visitAct?.Invoke(Zoom, x, y);
                    }
                }
            }
        }

        public bool Equals(MapTileMatrix other)
        {
            return Zoom == other.Zoom &&
                   Xmin == other.Xmin &&
                   Ymin == other.Ymin &&
                   Xmax == other.Xmax &&
                   Ymax == other.Ymax;
        }

        public override bool Equals(object? o)
        {
            return (o is MapTileMatrix other) && other.Equals(other);
        }

        public override int GetHashCode()
        {
            int hashCode = 2105055860;
            hashCode *= -1521134295 + Zoom.GetHashCode();
            hashCode *= -1521134295 + Xmin.GetHashCode();
            hashCode *= -1521134295 + Ymin.GetHashCode();
            hashCode *= -1521134295 + Xmax.GetHashCode();
            hashCode *= -1521134295 + Ymax.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(MapTileMatrix left, MapTileMatrix right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MapTileMatrix left, MapTileMatrix right)
        {
            return !(left == right);
        }
    }
}