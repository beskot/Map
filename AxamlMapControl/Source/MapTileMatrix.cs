using System;
using Avalonia;

namespace AxamlMapControl.Source
{
    internal sealed class MapTileMatrix : IEquatable<MapTileMatrix>
    {
        public int Zoom { get; }
        public int Xmin { get; }
        public int Ymin { get; }
        public int Xmax { get; }
        public int Ymax { get; }
        private readonly double _tileWidth;
        private readonly double _tileHeight;

        public int NumTiles => Zoom < 0 ? 0 : 1 << Zoom;
        public static MapTileMatrix Empty => new(-1, 0, 0, 0, 0);
        public long XLength => Math.Max(0L, (long)Xmax - Xmin);
        public long YLength => Math.Max(0L, (long)Ymax - Ymin);
        public long Length => checked(XLength * YLength);
        public Point Origin => new(Xmin * _tileWidth, Ymin * _tileHeight);
        public bool IsEmpty => Zoom < 0 || XLength == 0 || YLength == 0;

        public MapTileMatrix(int zoom, int xmin, int ymin, int xmax, int ymax,
            double tileWidth = MapTileLayer.TileSize, double tileHeight = MapTileLayer.TileSize)
        {
            if (zoom < -1 || zoom > 30) throw new ArgumentOutOfRangeException(nameof(zoom));
            ValidateSize(tileWidth, nameof(tileWidth));
            ValidateSize(tileHeight, nameof(tileHeight));
            Zoom = zoom;
            Xmin = xmin;
            Ymin = ymin;
            Xmax = xmax;
            Ymax = ymax;
            _tileWidth = tileWidth;
            _tileHeight = tileHeight;
        }

        private static void ValidateSize(double value, string name)
        {
            if (!double.IsFinite(value) || value <= 0)
                throw new ArgumentOutOfRangeException(name);
        }

        private static int TileIndex(double value)
        {
            if (!double.IsFinite(value) || value < int.MinValue || value > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value),
                    "Tile index exceeds the supported coordinate range.");
            return (int)value;
        }

        public static MapTileMatrix Create(int zoom, Rect bounds, double tileWidth, double tileHeight)
        {
            if (zoom < 0 || zoom > 30) throw new ArgumentOutOfRangeException(nameof(zoom));
            ValidateSize(tileWidth, nameof(tileWidth));
            ValidateSize(tileHeight, nameof(tileHeight));
            if (!double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) ||
                !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height) ||
                !double.IsFinite(bounds.Right) || !double.IsFinite(bounds.Bottom))
                throw new ArgumentOutOfRangeException(nameof(bounds));
            if (bounds.Width <= 0 || bounds.Height <= 0) return Empty;

            var count = 1 << zoom;
            // End indices are exclusive. X remains unbounded for horizontal wrapping.
            var xmin = TileIndex(Math.Floor(bounds.X / tileWidth));
            var xmax = TileIndex(Math.Ceiling(bounds.Right / tileWidth));
            var ymin = (int)Math.Clamp(Math.Floor(bounds.Y / tileHeight), 0d, count);
            var ymax = (int)Math.Clamp(Math.Ceiling(bounds.Bottom / tileHeight), 0d, count);
            return new MapTileMatrix(zoom, xmin, ymin, xmax, ymax, tileWidth, tileHeight);
        }

        public static MapTileMatrix Create(int zoom, Rect bounds, double tileSize) =>
            Create(zoom, bounds, tileSize, tileSize);

        public void TraverseСells(Action<int, int, int> visitAct)
        {
            if (IsEmpty || visitAct is null) return;
            for (var y = Ymin; y < Ymax; y++)
                for (var x = Xmin; x < Xmax; x++)
                    visitAct(Zoom, x, y);
        }

        public bool Equals(MapTileMatrix? other) => other is { } &&
            Zoom == other.Zoom && Xmin == other.Xmin && Ymin == other.Ymin &&
            Xmax == other.Xmax && Ymax == other.Ymax &&
            _tileWidth.Equals(other._tileWidth) && _tileHeight.Equals(other._tileHeight);

        public override bool Equals(object? obj) => obj is MapTileMatrix other && Equals(other);
        public override int GetHashCode() =>
            HashCode.Combine(Zoom, Xmin, Ymin, Xmax, Ymax, _tileWidth, _tileHeight);
        public static bool operator ==(MapTileMatrix? left, MapTileMatrix? right) =>
            ReferenceEquals(left, right) || (left is { } && left.Equals(right));
        public static bool operator !=(MapTileMatrix? left, MapTileMatrix? right) => !(left == right);
    }
}
