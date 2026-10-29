using System;
using System.Collections.Generic;
using AxamlMapControl.Source;
using Xunit;

namespace AxamlMapControlTests
{
    public class TileEqualityTests
    {
        [Fact]
        public void NullComparisonsDoNotThrow()
        {
            var tile = new Tile(3, 2, 1);
            Tile missing = null!;
            Assert.False(tile.Equals((Tile)null!));
            Assert.False(tile.Equals((object)null!));
            Assert.False(tile == missing);
            Assert.False(missing == tile);
            Assert.True(tile != missing);
            Assert.True(missing != tile);
            Assert.True(missing == null);
            Assert.False(missing != null);
        }

        [Fact]
        public void EquivalentCoordinatesHaveEqualHashesAndInterfaceEquality()
        {
            var first = new Tile(3, 2, 1);
            var second = new Tile(3, 2, 1);
            Assert.True(first.Equals((object)second));
            Assert.True(((IEquatable<Tile>)first).Equals(second));
            Assert.True(first == second);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
            Assert.False(first.Equals("3/2/1"));
        }

        [Theory]
        [InlineData(4, 2, 1)]
        [InlineData(3, 3, 1)]
        [InlineData(3, 2, 2)]
        public void EachCoordinateParticipatesInIdentity(int zoom, int x, int y)
        {
            var first = new Tile(3, 2, 1);
            var second = new Tile(zoom, x, y);
            Assert.False(first.Equals(second));
            Assert.True(first != second);
        }

        [Fact]
        public void DictionaryFindsAnEquivalentTileWithDifferentImageAndWorldCopy()
        {
            var key = new Tile(3, 2, 1, new byte[] { 1 }) { XIndex = 2 };
            var equivalent = new Tile(3, 2, 1, new byte[] { 9 }) { XIndex = 10 };
            var values = new Dictionary<Tile, string> { [key] = "found" };
            Assert.Equal("found", values[equivalent]);
            Assert.Single(new HashSet<Tile> { key, equivalent });
        }

        [Fact]
        public void UpdatingImageAndDisplayPositionDoesNotInvalidateDictionaryKey()
        {
            var key = new Tile(3, 2, 1);
            var values = new Dictionary<Tile, string> { [key] = "found" };
            var originalHash = key.GetHashCode();
            key.ImageData = new byte[] { 1, 2, 3 };
            key.XIndex = -6;
            Assert.Equal(originalHash, key.GetHashCode());
            Assert.Equal("found", values[new Tile(3, 2, 1)]);
        }
    }
}
