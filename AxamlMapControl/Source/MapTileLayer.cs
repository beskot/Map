using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AxamlMapControl.Source.Cache;

namespace AxamlMapControl.Source
{
    public class MapTileLayer : MapBaseLayer
    {
        public static readonly StyledProperty<ITileImageLoader> TileImageLoaderProperty =
            AvaloniaProperty.Register<MapTileLayer, ITileImageLoader>(nameof(TileImageLoader));
        public static readonly StyledProperty<ITileSource> TileSourceProperty =
            AvaloniaProperty.Register<MapTileLayer, ITileSource>(nameof(TileSource));

        public ITileImageLoader TileImageLoader
        {
            get => GetValue(TileImageLoaderProperty);
            set => SetValue(TileImageLoaderProperty, value);
        }
        public ITileSource TileSource
        {
            get => GetValue(TileSourceProperty);
            set => SetValue(TileSourceProperty, value);
        }

        public const int TileSize = 256;
        private MapTileMatrix _currentTileMatrix = MapTileMatrix.Empty;
        private List<Tile> _tileList = new();
        // Array keys use reference equality: repeated world copies share one bitmap.
        private readonly Dictionary<byte[], Bitmap> _images = new();
        private readonly HashSet<byte[]> _failedImages = new();
        private AvaloniaObject? _observedSource;
        private CancellationTokenSource? _loadCancellation;
        private long _generation;

        static MapTileLayer()
        {
            BackgroundProperty.OverrideDefaultValue<MapTileLayer>(Brushes.Transparent);
            TileSourceProperty.Changed.AddClassHandler<MapTileLayer>((layer, _) => layer.ResetTiles());
            TileImageLoaderProperty.Changed.AddClassHandler<MapTileLayer>((layer, _) => layer.ResetTiles());
        }

        public MapTileLayer()
        {
            TileImageLoader = new TileImageCache();
            TileSource = new TileSource();
            DetachedFromVisualTree += (_, _) =>
            {
                ObserveSource(null);
                _generation++;
                CancelPendingLoads();
                _tileList = new List<Tile>();
                _currentTileMatrix = MapTileMatrix.Empty;
                ClearImages();
                InvalidateVisual();
            };
        }

        private void CancelPendingLoads()
        {
            var cancellation = _loadCancellation;
            _loadCancellation = null;
            if (cancellation is null) return;
            try { cancellation.Cancel(); }
            catch (AggregateException ex)
            {
                Trace.TraceError("Tile cancellation callback failed: {0}", ex);
            }
            // Disposed on the UI thread only after the batch finishes.
        }

        private void ObserveSource(AvaloniaObject? source)
        {
            if (ReferenceEquals(source, _observedSource)) return;
            if (_observedSource is { } previous) previous.PropertyChanged -= SourcePropertyChanged;
            _observedSource = source;
            if (source is { }) source.PropertyChanged += SourcePropertyChanged;
        }

        private void SourcePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (ReferenceEquals(sender, _observedSource)) ResetTiles();
        }

        private void ResetTiles()
        {
            _generation++;
            CancelPendingLoads();
            _tileList = new List<Tile>();
            _currentTileMatrix = MapTileMatrix.Empty;
            ClearImages();
            ObserveSource(MapParent is null ? null : TileSource as AvaloniaObject);
            InvalidateVisual();
            if (MapParent is { } map && !map.Bounds.IsEmpty) OnViewChanged(map.ZoomLevel);
        }

        private void ClearImages()
        {
            foreach (var image in _images.Values) image.Dispose();
            _images.Clear();
            _failedImages.Clear();
        }

        // Called only from UI updates, never from Render or a loader worker.
        private void SynchronizeImages()
        {
            var visible = new HashSet<byte[]>(_tileList
                .Where(tile => tile.ImageData is { Length: > 0 })
                .Select(tile => tile.ImageData!));
            foreach (var data in _images.Keys.Where(data => !visible.Contains(data)).ToList())
            {
                _images[data].Dispose();
                _images.Remove(data);
            }
            _failedImages.IntersectWith(visible);
            foreach (var data in visible)
            {
                if (_images.ContainsKey(data) || _failedImages.Contains(data)) continue;
                try
                {
                    using var stream = new MemoryStream(data, writable: false);
                    var image = new Bitmap(stream);
                    try { _images.Add(data, image); }
                    catch { image.Dispose(); throw; }
                }
                catch (Exception ex)
                {
                    _failedImages.Add(data);
                    Trace.TraceError("Tile image decoding failed: {0}", ex);
                }
            }
        }

        public override void Render(DrawingContext context)
        {
            foreach (var tile in _tileList)
            {
                if (tile.ImageData is not { } data || !_images.TryGetValue(data, out var image)) continue;
                context.DrawImage(image,
                    new Rect((tile.XIndex - _currentTileMatrix.Xmin) * TileSize,
                        (tile.Y - _currentTileMatrix.Ymin) * TileSize, TileSize, TileSize));
            }
        }

        protected override void OnViewChanged(int zoomLevel)
        {
            try { UpdateView(zoomLevel); }
            catch (Exception ex) { Trace.TraceError("Tile layer update failed: {0}", ex); }
        }

        private void UpdateView(int zoomLevel)
        {
            if (MapParent is null || MapParent.Bounds.IsEmpty) return;
            var source = TileSource;
            var loader = TileImageLoader;
            ObserveSource(source as AvaloniaObject);
            var matrix = MapTileMatrix.Create(zoomLevel, MapParent.Transform.GetViewBounds(), TileSize);
            var changed = matrix != _currentTileMatrix || matrix.Length != _currentTileMatrix.Length;
            if (changed)
            {
                _generation++;
                CancelPendingLoads();
                var previous = _tileList;
                var next = new List<Tile>();
                matrix.TraverseСells((zoom, x, y) =>
                {
                    var tileX = ViewTransform.NormalizeValue(x, matrix.NumTiles);
                    var old = previous.Find(tile => tile.Zoom == zoom && tile.X == tileX &&
                        tile.Y == y && tile.XIndex == x);
                    next.Add(new Tile(zoom, tileX, y)
                    {
                        XIndex = x,
                        ImageData = old?.ImageData
                    });
                });
                _currentTileMatrix = matrix;
                _tileList = next;
                SynchronizeImages();
            }

            RenderTransform = new MatrixTransform(Matrix.CreateTranslation(
                ViewTopLeft + _currentTileMatrix.Origin + PointTranslated));
            InvalidateVisual();
            if (!changed || source is null || loader is null) return;
            if (source is TileSource && string.IsNullOrWhiteSpace(source.UriStringFormat)) return;
            // Workers receive separate objects; visible tile data is published on the UI thread.
            var pending = _tileList.Where(tile => tile.ImageData is null)
                .Select(tile => new Tile(tile.Zoom, tile.X, tile.Y) { XIndex = tile.XIndex })
                .ToList();
            if (pending.Count == 0) return;
            var snapshot = new ResolvedSource(source, pending);
            var cancellation = new CancellationTokenSource();
            _loadCancellation = cancellation;
            _ = LoadTilesAsync(loader, snapshot, pending, _generation, cancellation);
        }

        private async Task LoadTilesAsync(ITileImageLoader loader, ITileSource source,
            List<Tile> tiles, long generation, CancellationTokenSource cancellation)
        {
            var token = cancellation.Token;
            try
            {
                await loader.TileLoadAsync(tiles, source, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                Dispatcher.UIThread.Post(() =>
                {
                    if (token.IsCancellationRequested || generation != _generation || MapParent is null) return;
                    try
                    {
                        var loaded = new Dictionary<Tile, byte[]>();
                        foreach (var tile in tiles)
                            if (tile.ImageData is { Length: > 0 } data) loaded[tile] = data;
                        foreach (var tile in _tileList)
                            if (tile.ImageData is null && loaded.TryGetValue(tile, out var data))
                                tile.ImageData = data;
                        SynchronizeImages();
                        InvalidateVisual();
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("Publishing tile images failed: {0}", ex);
                    }
                });
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex)
            {
                Trace.TraceError("Tile batch failed: {0}", ex);
            }
            finally
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (ReferenceEquals(_loadCancellation, cancellation)) _loadCancellation = null;
                    cancellation.Dispose();
                });
            }
        }

        private sealed class ResolvedSource : ITileSource
        {
            private readonly Dictionary<Tile, Uri> _uris = new();
            public string UriStringFormat { get; }
            public ResolvedSource(ITileSource source, IEnumerable<Tile> tiles)
            {
                UriStringFormat = source.UriStringFormat;
                foreach (var tile in tiles) _uris[tile] = source.GetUri(tile);
            }
            public Uri GetUri(Tile tile) => _uris[tile];
        }
    }
}
