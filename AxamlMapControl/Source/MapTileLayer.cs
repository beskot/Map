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
        private const int ParallelLoads = 4;
        private MapTileMatrix _currentTileMatrix = MapTileMatrix.Empty;
        private List<Tile> _tileList = new();
        private List<Tile> _fallbackTiles = new();
        private readonly Dictionary<byte[], Bitmap> _images = new();
        private readonly HashSet<byte[]> _failedImages = new();
        private readonly Dictionary<Tile, CancellationTokenSource> _requests = new();
        private readonly HashSet<Tile> _failedRequests = new();
        private readonly LayerDrawingSurface _tileSurface;
        private AvaloniaObject? _observedSource;
        private long _generation;
        private int _activeLoads;

        static MapTileLayer()
        {
            BackgroundProperty.OverrideDefaultValue<MapTileLayer>(Brushes.Transparent);
            TileSourceProperty.Changed.AddClassHandler<MapTileLayer>((layer, _) => layer.ResetTiles());
            TileImageLoaderProperty.Changed.AddClassHandler<MapTileLayer>((layer, _) => layer.ResetTiles());
        }

        public MapTileLayer()
        {
            _tileSurface = new LayerDrawingSurface(RenderTiles);
            Children.Add(_tileSurface);
            TileImageLoader = new TileImageCache();
            TileSource = new TileSource();
            DetachedFromVisualTree += (_, _) =>
            {
                ObserveSource(null);
                ClearTiles();
                InvalidateTiles();
            };
        }

        private void InvalidateTiles()
        {
            _tileSurface?.InvalidateVisual();
            InvalidateVisual();
        }

        private void ClearTiles()
        {
            _generation++;
            foreach (var cancellation in _requests.Values.ToArray())
            {
                try { cancellation.Cancel(); }
                catch (AggregateException ex) { Trace.TraceError("Tile cancellation failed: {0}", ex); }
            }
            _requests.Clear();
            _failedRequests.Clear();
            _tileList.Clear();
            _fallbackTiles.Clear();
            _currentTileMatrix = MapTileMatrix.Empty;
            foreach (var image in _images.Values) image.Dispose();
            _images.Clear();
            _failedImages.Clear();
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
            ClearTiles();
            ObserveSource(MapParent is null ? null : TileSource as AvaloniaObject);
            InvalidateTiles();
            if (MapParent is { } map && map.Bounds.Width > 0 && map.Bounds.Height > 0)
                OnViewChanged(map.ZoomLevel);
        }

        private bool HasImage(Tile tile) => tile.ImageData is { } data && _images.ContainsKey(data);

        private void SynchronizeImages()
        {
            var visible = new HashSet<byte[]>(_tileList.Concat(_fallbackTiles)
                .Where(tile => tile.ImageData is { Length: > 0 }).Select(tile => tile.ImageData!));
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
            if (_fallbackTiles.Count > 0 && _tileList.Count > 0 && _tileList.All(HasImage))
            {
                _fallbackTiles.Clear();
                SynchronizeImages();
            }
        }

        private void RenderTiles(DrawingContext context)
        {
            if (_currentTileMatrix.IsEmpty) return;
            DrawTiles(context, _fallbackTiles);
            DrawTiles(context, _tileList);
        }

        private void DrawTiles(DrawingContext context, IEnumerable<Tile> tiles)
        {
            foreach (var tile in tiles)
            {
                if (tile.ImageData is not { } data || !_images.TryGetValue(data, out var image)) continue;
                var scale = Math.Pow(2d, _currentTileMatrix.Zoom - tile.Zoom);
                var size = TileSize * scale;
                context.DrawImage(image, new Rect(
                    (tile.XIndex * scale - _currentTileMatrix.Xmin) * TileSize,
                    (tile.Y * scale - _currentTileMatrix.Ymin) * TileSize, size, size));
            }
        }

        protected override void OnViewChanged(int zoomLevel)
        {
            try { UpdateView(zoomLevel); }
            catch (Exception ex) { Trace.TraceError("Tile layer update failed: {0}", ex); }
        }

        private void UpdateView(int zoomLevel)
        {
            if (MapParent is null || !(MapParent.Bounds.Width > 0 && MapParent.Bounds.Height > 0)) return;
            ObserveSource(TileSource as AvaloniaObject);
            var matrix = MapTileMatrix.Create(zoomLevel, MapParent.Transform.GetViewBounds(), TileSize);
            if (matrix != _currentTileMatrix)
            {
                var previous = _tileList;
                if (previous.Any(HasImage) && (_fallbackTiles.Count == 0 || previous.All(HasImage)))
                    _fallbackTiles = previous.Where(HasImage).ToList();
                var reusable = previous.Concat(_fallbackTiles)
                    .Where(tile => tile.ImageData is { Length: > 0 })
                    .GroupBy(tile => tile).ToDictionary(group => group.Key, group => group.First().ImageData);
                var next = new List<Tile>();
                matrix.TraverseСells((zoom, x, y) =>
                {
                    var tile = new Tile(zoom, ViewTransform.NormalizeValue(x, matrix.NumTiles), y)
                    { XIndex = x };
                    if (reusable.TryGetValue(tile, out var data)) tile.ImageData = data;
                    next.Add(tile);
                });
                _currentTileMatrix = matrix;
                _tileList = next;
                _failedRequests.Clear();
                SynchronizeImages();
            }
            RenderTransform = new MatrixTransform(Matrix.CreateTranslation(
                ViewTopLeft + _currentTileMatrix.Origin + PointTranslated));
            InvalidateTiles();
            PumpLoads();
        }

        // UI-thread scheduler: the latest viewport is the queue; in-flight work survives panning.
        private void PumpLoads()
        {
            if (MapParent is null || _currentTileMatrix.IsEmpty || TileSource is null || TileImageLoader is null) return;
            if (TileSource is TileSource && string.IsNullOrWhiteSpace(TileSource.UriStringFormat)) return;
            var centerX = (_currentTileMatrix.Xmin + (double)_currentTileMatrix.Xmax) / 2;
            var centerY = (_currentTileMatrix.Ymin + (double)_currentTileMatrix.Ymax) / 2;
            var pending = _tileList.Where(tile => tile.ImageData is null &&
                    !_requests.ContainsKey(tile) && !_failedRequests.Contains(tile))
                .OrderBy(tile => Math.Pow(tile.XIndex + 0.5 - centerX, 2) +
                    Math.Pow(tile.Y + 0.5 - centerY, 2))
                .Distinct().Take(Math.Max(0, ParallelLoads - _activeLoads)).ToList();
            foreach (var visible in pending)
            {
                var tile = new Tile(visible.Zoom, visible.X, visible.Y);
                ITileSource source;
                try { source = new ResolvedSource(TileSource, new[] { tile }); }
                catch (Exception ex)
                {
                    _failedRequests.Add(tile);
                    Trace.TraceError("Resolving tile URI failed: {0}", ex);
                    continue;
                }
                var cancellation = new CancellationTokenSource();
                _requests.Add(tile, cancellation);
                _activeLoads++;
                _ = LoadTileAsync(TileImageLoader, source, tile, _generation, cancellation);
            }
        }

        private async Task LoadTileAsync(ITileImageLoader loader, ITileSource source,
            Tile tile, long generation, CancellationTokenSource cancellation)
        {
            var token = cancellation.Token;
            try
            {
                // Each slot completes independently, including cache lookup and insertion.
                await Task.Run(() => loader.TileLoadAsync(new[] { tile }, source, token), token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex) { Trace.TraceError("Tile load failed: {0}", ex); }
            finally
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _activeLoads--;
                    if (_requests.TryGetValue(tile, out var current) && ReferenceEquals(current, cancellation))
                        _requests.Remove(tile);
                    try
                    {
                        if (!token.IsCancellationRequested && generation == _generation && MapParent is not null)
                        {
                            if (tile.ImageData is { Length: > 0 } data)
                            {
                                foreach (var visible in _tileList)
                                    if (visible.Equals(tile)) visible.ImageData = data;
                                SynchronizeImages();
                                InvalidateTiles();
                            }
                            else _failedRequests.Add(tile);
                        }
                    }
                    catch (Exception ex) { Trace.TraceError("Publishing tile failed: {0}", ex); }
                    finally
                    {
                        cancellation.Dispose();
                        try { PumpLoads(); }
                        catch (Exception ex) { Trace.TraceError("Scheduling tiles failed: {0}", ex); }
                    }
                }, DispatcherPriority.Normal);
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
