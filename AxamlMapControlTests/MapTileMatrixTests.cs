using System;
using System.Collections.Generic;
using Avalonia;
using AxamlMapControl.Source;
using Xunit;

namespace AxamlMapControlTests
{
    public class MapTileMatrixTests
    {
        [Fact]
        public void ObjectEqualityComparesBothMatrices()
        {
            var first = new MapTileMatrix(2, 0, 0, 1, 1);
            var same = new MapTileMatrix(2, 0, 0, 1, 1);
            var different = new MapTileMatrix(2, 1, 0, 2, 1);
            Assert.True(first.Equals((object)same));
            Assert.False(first.Equals((object)different));
            Assert.Equal(first.GetHashCode(), same.GetHashCode());
            Assert.True(first == same);
            Assert.True(first != different);
            Assert.False(first.Equals((object)null!));
            Assert.False(first == null);
            Assert.False(null == first);
            MapTileMatrix missing = null!;
            Assert.True(missing == null);
        }

        [Fact]
        public void ExactTileBoundaryDoesNotIncludeExtraTiles()
        {
            var matrix = MapTileMatrix.Create(3, new Rect(0, 0, 256, 256), 256);
            Assert.Equal(1L, matrix.XLength);
            Assert.Equal(1L, matrix.YLength);
            Assert.Equal(1L, matrix.Length);
        }

        [Theory]
        [InlineData(-768)]
        [InlineData(768)]
        public void OutsideVerticalWorldHasNoCells(double y)
        {
            var matrix = MapTileMatrix.Create(1, new Rect(0, y, 256, 256), 256);
            Assert.True(matrix.IsEmpty);
            Assert.Equal(0L, matrix.Length);
            var visits = 0;
            matrix.TraverseСells((_, _, _) => visits++);
            Assert.Equal(0, visits);
        }

        [Fact]
        public void NegativeHorizontalCoordinatesRemainAvailableForWrapping()
        {
            var matrix = MapTileMatrix.Create(2, new Rect(-256, 0, 256, 256), 256);
            var cells = new List<(int Zoom, int X, int Y)>();
            matrix.TraverseСells((z, x, y) => cells.Add((z, x, y)));
            Assert.Single(cells);
            Assert.Equal((2, -1, 0), cells[0]);
        }

        [Fact]
        public void OriginDoesNotOverflowAtHighZoom()
        {
            var x = 1 << 29;
            var matrix = new MapTileMatrix(30, x, 0, x + 1, 1);
            Assert.Equal((double)x * 256, matrix.Origin.X);
            Assert.Equal(1 << 30, matrix.NumTiles);
        }

        [Fact]
        public void RectangularTilesUseTheirOwnDimensionsForOrigin()
        {
            var matrix = MapTileMatrix.Create(2, new Rect(128, 64, 128, 64), 128, 64);
            Assert.Equal(new Point(128, 64), matrix.Origin);
            Assert.Equal(1L, matrix.Length);
        }

        [Fact]
        public void EmptyBoundsHaveNoTiles()
        {
            var matrix = MapTileMatrix.Create(2, new Rect(0, 0, 0, 256), 256);
            Assert.True(matrix.IsEmpty);
            Assert.Equal(0L, matrix.Length);
            Assert.Equal(0, matrix.NumTiles);
        }

        [Fact]
        public void InvalidZoomAndTileSizesAreRejected()
        {
            var bounds = new Rect(0, 0, 256, 256);
            Assert.Throws<ArgumentOutOfRangeException>(() => MapTileMatrix.Create(-1, bounds, 256));
            Assert.Throws<ArgumentOutOfRangeException>(() => MapTileMatrix.Create(31, bounds, 256));
            Assert.Throws<ArgumentOutOfRangeException>(() => MapTileMatrix.Create(2, bounds, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => MapTileMatrix.Create(2, bounds, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => MapTileMatrix.Create(2, bounds, double.PositiveInfinity));
        }
    }
}
