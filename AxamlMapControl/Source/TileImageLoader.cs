using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AxamlMapControl.Source
{
    public class TileImageLoader : ITileImageLoader
    {
        private const int TaskLimit = 4;
        private static readonly HttpClient SharedClient = new()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        private readonly SemaphoreSlim _requestSlots = new(TaskLimit, TaskLimit);

        public HttpClient HttpClient { get; }

        public TileImageLoader() : this(SharedClient) { }

        // The supplied client remains owned by the caller.
        public TileImageLoader(HttpClient httpClient)
        {
            HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public Task TileLoadAsync(IEnumerable<Tile> tiles, ITileSource source)
        {
            return TileLoadAsync(tiles, source, CancellationToken.None);
        }

        public async Task TileLoadAsync(
            IEnumerable<Tile> tiles, ITileSource source, CancellationToken cancellationToken)
        {
            if (tiles is null) throw new ArgumentNullException(nameof(tiles));
            if (source is null) throw new ArgumentNullException(nameof(source));
            cancellationToken.ThrowIfCancellationRequested();

            var queue = new ConcurrentQueue<Tile>(tiles);
            var workers = Enumerable.Range(0, Math.Min(queue.Count, TaskLimit))
                .Select(_ => LoadTilesAsync(queue, source, cancellationToken))
                .ToArray();
            await Task.WhenAll(workers).ConfigureAwait(false);
        }

        private async Task LoadTilesAsync(
            ConcurrentQueue<Tile> queue, ITileSource source, CancellationToken cancellationToken)
        {
            while (queue.TryDequeue(out var tile))
            {
                await _requestSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var uri = source.GetUri(tile);
                    using var response = await HttpClient.GetAsync(
                        uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                        .ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        var data = await response.Content.ReadAsByteArrayAsync(cancellationToken)
                            .ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                        tile.ImageData = data;
                    }
                    else
                    {
                        Trace.TraceWarning(
                            "Tile request {0} returned HTTP {1}.", uri, (int)response.StatusCode);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Trace.TraceError(
                        "Tile load failed for z={0}, x={1}, y={2}: {3}",
                        tile.Zoom, tile.X, tile.Y, ex);
                }
                finally
                {
                    _requestSlots.Release();
                }
            }
        }
    }
}
