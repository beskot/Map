using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AxamlMapControl.Source;
using Xunit;

namespace AxamlMapControlTests
{
    public class TileImageLoaderTests
    {
        [Fact]
        public async Task LoadsFourRequestsConcurrentlyAndKeepsBatchesSeparate()
        {
            using var handler = new GatedHandler();
            using var client = new HttpClient(handler);
            var loader = new TileImageLoader(client);
            var first = Enumerable.Range(0, 8).Select(x => new Tile(3, x, 0)).ToArray();
            var second = Enumerable.Range(0, 8).Select(x => new Tile(3, x, 1)).ToArray();

            var firstLoad = loader.TileLoadAsync(first, new TestSource("first.example"));
            var secondLoad = loader.TileLoadAsync(second, new TestSource("second.example"));
            try
            {
                var ready = await Task.WhenAny(handler.FourStarted.Task, Task.Delay(5000));
                Assert.Same(handler.FourStarted.Task, ready);
                Assert.Equal(4, handler.MaximumActive);
            }
            finally
            {
                handler.Release.TrySetResult(true);
                await Task.WhenAll(firstLoad, secondLoad);
            }

            Assert.All(first, tile => Assert.Equal(new byte[] { 1 }, tile.ImageData));
            Assert.All(second, tile => Assert.Equal(new byte[] { 2 }, tile.ImageData));
            Assert.Equal(4, handler.MaximumActive);
        }

        [Fact]
        public async Task CancellationReleasesSlotsForTheNextBatch()
        {
            using var handler = new GatedHandler();
            using var client = new HttpClient(handler);
            var loader = new TileImageLoader(client);
            using var cancellation = new CancellationTokenSource();
            var cancelledTiles = Enumerable.Range(0, 8)
                .Select(x => new Tile(3, x, 0)).ToArray();
            var load = loader.TileLoadAsync(
                cancelledTiles, new TestSource("first.example"), cancellation.Token);

            try
            {
                var ready = await Task.WhenAny(handler.FourStarted.Task, Task.Delay(5000));
                Assert.Same(handler.FourStarted.Task, ready);
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load);
                Assert.All(cancelledTiles, tile => Assert.Null(tile.ImageData));
            }
            finally
            {
                cancellation.Cancel();
                handler.Release.TrySetResult(true);
                try { await load; } catch (OperationCanceledException) { }
            }

            var next = new Tile(0, 0, 0);
            await loader.TileLoadAsync(new[] { next }, new TestSource("second.example"));
            Assert.Equal(new byte[] { 2 }, next.ImageData);
        }

        private sealed class TestSource : ITileSource
        {
            private readonly string _host;
            public string UriStringFormat => "https://" + _host + "/{z}/{x}/{y}.png";
            public TestSource(string host) { _host = host; }
            public Uri GetUri(Tile tile) => new Uri(
                $"https://{_host}/{tile.Zoom}/{tile.X}/{tile.Y}.png");
        }

        private sealed class GatedHandler : HttpMessageHandler
        {
            private int _active;
            private int _maximumActive;
            public int MaximumActive => Volatile.Read(ref _maximumActive);
            public TaskCompletionSource<bool> FourStarted { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<bool> Release { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var active = Interlocked.Increment(ref _active);
                int observed;
                do
                {
                    observed = Volatile.Read(ref _maximumActive);
                    if (active <= observed) break;
                } while (Interlocked.CompareExchange(
                    ref _maximumActive, active, observed) != observed);

                if (active >= 4) FourStarted.TrySetResult(true);
                try
                {
                    var cancelled = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    using var registration = cancellationToken.Register(
                        () => cancelled.TrySetResult(true));
                    await Task.WhenAny(Release.Task, cancelled.Task);
                    cancellationToken.ThrowIfCancellationRequested();
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(new byte[]
                        {
                            request.RequestUri.Host == "first.example" ? (byte)1 : (byte)2
                        })
                    };
                }
                finally
                {
                    Interlocked.Decrement(ref _active);
                }
            }
        }
    }
}
