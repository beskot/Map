using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;

namespace AxamlMapControl.Source.Cache
{
    /// <summary>SQLite cache using full tile URIs and operation-scoped connections.</summary>
    public class TileImageSqliteCache : AvaloniaObject, ITileImageLoader, IDisposable
    {
        public static readonly StyledProperty<string> DataSourceProperty =
            AvaloniaProperty.Register<TileImageSqliteCache, string>(nameof(DataSource), string.Empty);

        private string _dataSource = TileContext.DefaultDataSource;
        private int _disposed;
        public ITileImageLoader TileImageLoader { get; }

        public string DataSource
        {
            get => Volatile.Read(ref _dataSource);
            set
            {
                ThrowIfDisposed();
                SetValue(DataSourceProperty, value);
            }
        }

        static TileImageSqliteCache()
        {
            DataSourceProperty.Changed.AddClassHandler<TileImageSqliteCache>((cache, e) =>
            {
                var path = e.NewValue is string value && !string.IsNullOrWhiteSpace(value)
                    ? Path.GetFullPath(value) : TileContext.DefaultDataSource;
                Volatile.Write(ref cache._dataSource, path);
            });
        }

        public TileImageSqliteCache() : this(new TileImageLoader()) { }

        // The supplied loader remains owned by the caller.
        public TileImageSqliteCache(ITileImageLoader tileImageLoader)
        {
            TileImageLoader = tileImageLoader ?? throw new ArgumentNullException(nameof(tileImageLoader));
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(TileImageSqliteCache));
        }

        public Task TileLoadAsync(IEnumerable<Tile> tiles, ITileSource source) =>
            TileLoadAsync(tiles, source, CancellationToken.None);

        public async Task TileLoadAsync(IEnumerable<Tile> tiles, ITileSource source,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (tiles is null) throw new ArgumentNullException(nameof(tiles));
            if (source is null) throw new ArgumentNullException(nameof(source));
            cancellationToken.ThrowIfCancellationRequested();
            var store = new SqliteTileStore(DataSource);
            var requests = tiles.Select(tile =>
                (Tile: tile, Key: source.GetUri(tile).ToString())).ToList();
            var cached = await store.ReadAsync(requests.Select(request => request.Key), cancellationToken)
                .ConfigureAwait(false);
            foreach (var request in requests)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (cached.TryGetValue(request.Key, out var data)) request.Tile.ImageData = data;
            }

            var missing = requests.Where(request => request.Tile.ImageData is null).ToList();
            if (missing.Count == 0) return;
            await TileImageLoader.TileLoadAsync(
                missing.Select(request => request.Tile).ToList(), source, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var images = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var request in missing)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (request.Tile.ImageData is { Length: > 0 } data) images[request.Key] = data;
            }
            await store.WriteAsync(images, cancellationToken).ConfigureAwait(false);
        }

        // No connection is retained. Already-started operations finish with their own token.
        public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
    }
}
