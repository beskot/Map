using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AxamlMapControl.Source;
using AxamlMapControl.Source.Cache;
using Xunit;

namespace AxamlMapControlTests
{
    public class TileImageCacheTests
    {
        [Fact]
        public async Task ReusesSuccessfulDataWithoutEnumeratingInputTwice()
        {
            var loader = new StubLoader(new byte[] { 1, 2 });
            using var cache = new TileImageCache(loader, 1024);
            var source = new TestSource();
            var enumerations = 0;
            IEnumerable<Tile> Once()
            {
                if (++enumerations > 1) throw new InvalidOperationException();
                yield return new Tile(0, 0, 0);
            }
            await cache.TileLoadAsync(Once(), source);
            var next = new Tile(0, 0, 0);
            await cache.TileLoadAsync(new[] { next }, source);
            Assert.Equal(1, enumerations);
            Assert.Equal(1, loader.Calls);
            Assert.Equal(new byte[] { 1, 2 }, next.ImageData);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        public async Task DoesNotCacheEmptyOrOversizedData(int length)
        {
            var loader = new StubLoader(new byte[length]);
            using var cache = new TileImageCache(loader, 4);
            var source = new TestSource();
            await cache.TileLoadAsync(new[] { new Tile(0, 0, 0) }, source);
            await cache.TileLoadAsync(new[] { new Tile(0, 0, 0) }, source);
            Assert.Equal(2, loader.Calls);
        }

        [Fact]
        public async Task RetriesFailedLoads()
        {
            var loader = new StubLoader(null);
            using var cache = new TileImageCache(loader, 4);
            var source = new TestSource();
            await cache.TileLoadAsync(new[] { new Tile(0, 0, 0) }, source);
            loader.Data = new byte[] { 3 };
            var next = new Tile(0, 0, 0);
            await cache.TileLoadAsync(new[] { next }, source);
            Assert.Equal(2, loader.Calls);
            Assert.Equal(new byte[] { 3 }, next.ImageData);
        }

        private sealed class StubLoader : ITileImageLoader
        {
            public byte[] Data { get; set; }
            public int Calls { get; private set; }
            public StubLoader(byte[] data) { Data = data; }
            public Task TileLoadAsync(IEnumerable<Tile> tiles, ITileSource source)
            {
                Calls++;
                foreach (var tile in tiles.ToList()) tile.ImageData = Data;
                return Task.CompletedTask;
            }
        }

        private sealed class TestSource : ITileSource
        {
            public string UriStringFormat => "https://tiles.example/{z}/{x}/{y}.png";
            public Uri GetUri(Tile tile) => new Uri(
                $"https://tiles.example/{tile.Zoom}/{tile.X}/{tile.Y}.png");
        }
    }
}
