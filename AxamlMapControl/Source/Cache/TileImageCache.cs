using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace AxamlMapControl.Source.Cache
{
    /// <summary>Memory cache limited by the number of encoded image bytes.</summary>
    public class TileImageCache : ITileImageLoader, IDisposable
    {
        private const long DefaultSizeLimit = 64L * 1024 * 1024;
        private readonly ITileImageLoader _tileLoader;
        private readonly MemoryCache _cache;
        private readonly long _sizeLimit;

        public TileImageCache() : this(new TileImageLoader(), DefaultSizeLimit) { }

        // The supplied loader remains owned by the caller.
        public TileImageCache(ITileImageLoader tileLoader, long sizeLimit = DefaultSizeLimit)
        {
            _tileLoader = tileLoader ?? throw new ArgumentNullException(nameof(tileLoader));
            if (sizeLimit <= 0) throw new ArgumentOutOfRangeException(nameof(sizeLimit));
            _sizeLimit = sizeLimit;
            _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = sizeLimit });
        }

        public Task TileLoadAsync(IEnumerable<Tile> tiles, ITileSource source) =>
            TileLoadAsync(tiles, source, CancellationToken.None);

        public async Task TileLoadAsync(IEnumerable<Tile> tiles, ITileSource source,
            CancellationToken cancellationToken)
        {
            if (tiles is null) throw new ArgumentNullException(nameof(tiles));
            if (source is null) throw new ArgumentNullException(nameof(source));
            cancellationToken.ThrowIfCancellationRequested();
            var requests = tiles.Select(tile =>
                (Tile: tile, Key: source.GetUri(tile).ToString())).ToList();
            foreach (var request in requests)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_cache.TryGetValue(request.Key, out byte[] data))
                    request.Tile.ImageData = data;
            }

            var missing = requests.Where(request => request.Tile.ImageData is null).ToList();
            cancellationToken.ThrowIfCancellationRequested();
            if (missing.Count == 0) return;
            await _tileLoader.TileLoadAsync(
                missing.Select(request => request.Tile).ToList(), source, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var request in missing)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var data = request.Tile.ImageData;
                if (data is null || data.Length == 0 || data.LongLength > _sizeLimit) continue;
                _cache.Set(request.Key, data, new MemoryCacheEntryOptions()
                    .SetSize(data.LongLength)
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10)));
            }
        }

        public void Dispose() => _cache.Dispose();
    }
}
