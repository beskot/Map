using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AxamlMapControl.Source;
using AxamlMapControl.Source.Cache;
using Xunit;

namespace AxamlMapControlTests
{
    public class TileCancellationTests
    {
        [Fact]
        public async Task PreCancelledMemoryRequestDoesNotCallLoader()
        {
            var probe = new ProbeLoader();
            using var cache = new TileImageCache(probe, 1024);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                cache.TileLoadAsync(new[] { new Tile(0, 0, 0) }, new TestSource(), cancellation.Token));
            Assert.Equal(0, probe.Calls);
        }

        [Fact]
        public async Task MemoryCacheForwardsCancellationAndDoesNotCachePartialData()
        {
            var probe = new ProbeLoader();
            using var cache = new TileImageCache(probe, 1024);
            using var cancellation = new CancellationTokenSource();
            var load = cache.TileLoadAsync(
                new[] { new Tile(0, 0, 0) }, new TestSource(), cancellation.Token);
            try
            {
                await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load);
            }
            finally
            {
                cancellation.Cancel();
                try { await load; } catch (OperationCanceledException) { }
            }
            probe.WaitForCancellation = false;
            var next = new Tile(0, 0, 0);
            await cache.TileLoadAsync(new[] { next }, new TestSource());
            Assert.Equal(2, probe.Calls);
            Assert.Equal(new byte[] { 2 }, next.ImageData);
        }

        [Fact]
        public async Task LegacyLoaderWorksThroughTheNewInterfaceOverload()
        {
            ITileImageLoader loader = new LegacyLoader();
            var tile = new Tile(0, 0, 0);
            await loader.TileLoadAsync(new[] { tile }, new TestSource(), CancellationToken.None);
            Assert.Equal(new byte[] { 7 }, tile.ImageData);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                loader.TileLoadAsync(new[] { new Tile(0, 0, 0) }, new TestSource(), cancellation.Token));
        }

        private sealed class ProbeLoader : ITileImageLoader
        {
            public int Calls { get; private set; }
            public bool WaitForCancellation { get; set; } = true;
            public TaskCompletionSource<bool> Started { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task TileLoadAsync(IEnumerable<Tile> tiles, ITileSource source) =>
                throw new InvalidOperationException("The cache must call the cancellable overload.");
            public async Task TileLoadAsync(IEnumerable<Tile> tiles, ITileSource source,
                CancellationToken cancellationToken)
            {
                Calls++;
                foreach (var tile in tiles) tile.ImageData = new byte[] { (byte)Calls };
                Started.TrySetResult(true);
                if (WaitForCancellation)
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private sealed class LegacyLoader : ITileImageLoader
        {
            public Task TileLoadAsync(IEnumerable<Tile> tiles, ITileSource source)
            {
                foreach (var tile in tiles) tile.ImageData = new byte[] { 7 };
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
