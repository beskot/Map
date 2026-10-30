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

        internal void InvalidateZoomAnimation() => _tileSurface.InvalidateVisual();

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

        // UI-thread ownership and eviction only; image decoding happens in LoadTileAsync.
        private void SynchronizeImages()
        {
            if (_fallbackTiles.Count > 0 && _tileList.Count > 0 && _tileList.All(HasImage))
                _fallbackTiles.Clear();
            var visible = new HashSet<byte[]>(_tileList.Concat(_fallbackTiles)
                .Where(tile => tile.ImageData is { Length: > 0 }).Select(tile => tile.ImageData!));
            foreach (var data in _images.Keys.Where(data => !visible.Contains(data)).ToList())
            {
                _images[data].Dispose();
                _images.Remove(data);
            }
        }

        private Matrix GetTileAnimationMatrix()
        {
            if (MapParent is not { } map || !map.TryGetTileAnimation(out var scale, out var origin))
                return Matrix.Identity;
            var offset = ViewTopLeft + _currentTileMatrix.Origin + PointTranslated;
            var center = map.Transform.ViewCenter;
            return new Matrix(scale, 0, 0, scale,
                origin.X + (offset.X - center.X) * scale - offset.X,
                origin.Y + (offset.Y - center.Y) * scale - offset.Y);
        }

        private void RenderTiles(DrawingContext context)
        {
            if (_currentTileMatrix.IsEmpty || MapParent is { SuppressTileCapture: true }) return;
            // Retain complete edge tiles so translation-only panning does not expose holes.
            var bounds = new Rect(0, 0, _currentTileMatrix.XLength * (double)TileSize,
                _currentTileMatrix.YLength * (double)TileSize);
            using (context.PushTransform(GetTileAnimationMatrix()))
            using (context.PushClip(bounds))
            {
                DrawTiles(context, _fallbackTiles, bounds);
                DrawTiles(context, _tileList, bounds);
            }
        }

        private void DrawTiles(DrawingContext context, IEnumerable<Tile> tiles, Rect bounds)
        {
            var previousZoom = -1;
            var scale = 1d;
            foreach (var tile in tiles)
            {
                if (tile.Zoom > _currentTileMatrix.Zoom) continue;
                if (tile.ImageData is not { } data || !_images.TryGetValue(data, out var image)) continue;
                if (tile.Zoom != previousZoom)
                {
                    previousZoom = tile.Zoom;
                    scale = Math.Pow(2d, _currentTileMatrix.Zoom - tile.Zoom);
                }
                var size = TileSize * scale;
                var x = (tile.XIndex * scale - _currentTileMatrix.Xmin) * TileSize;
                var y = (tile.Y * scale - _currentTileMatrix.Ymin) * TileSize;
                var left = Math.Max(x, bounds.X);
                var top = Math.Max(y, bounds.Y);
                var right = Math.Min(x + size, bounds.Right);
                var bottom = Math.Min(y + size, bounds.Bottom);
                if (right <= left || bottom <= top) continue;
                var destination = new Rect(left, top, right - left, bottom - top);
                var pixels = image.PixelSize;
                var source = new Rect((left - x) / size * pixels.Width,
                    (top - y) / size * pixels.Height,
                    destination.Width / size * pixels.Width,
                    destination.Height / size * pixels.Height);
                context.DrawImage(image, source, destination);
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
            var matrixChanged = matrix != _currentTileMatrix;
            if (matrixChanged)
            {
                var previous = _tileList;
                var zoomingOut = !_currentTileMatrix.IsEmpty && matrix.Zoom < _currentTileMatrix.Zoom;
                if (zoomingOut)
                    _fallbackTiles.Clear();
                else if (previous.Any(HasImage) && (_fallbackTiles.Count == 0 || previous.All(HasImage)))
                    _fallbackTiles = previous.Where(HasImage).ToList();
                _fallbackTiles.RemoveAll(tile => tile.Zoom > matrix.Zoom);
                var reusable = previous.Concat(_fallbackTiles)
                    .Where(HasImage)
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
            // A translation moves retained content without rebuilding the tile drawing.
            if (matrixChanged)
            {
                InvalidateTiles();
                PumpLoads();
            }
        }

        // UI-thread scheduler: the latest viewport is the queue; in-flight work survives panning.
        private void PumpLoads()
        {
            if (_activeLoads >= ParallelLoads) return;
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
            Bitmap? decoded = null;
            try
            {
                decoded = await Task.Run(async () =>
                {
                    await loader.TileLoadAsync(new[] { tile }, source, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    if (tile.ImageData is not { Length: > 0 } data) return (Bitmap?)null;
                    using var stream = new MemoryStream(data, writable: false);
                    return new Bitmap(stream);
                }, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex) { Trace.TraceError("Tile load or decode failed: {0}", ex); }
            finally
            {
                try
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
                                if (decoded is not null && tile.ImageData is { Length: > 0 } data)
                                {
                                    var matches = _tileList.Where(visible => visible.Equals(tile)).ToList();
                                    if (matches.Count > 0)
                                    {
                                        if (!_images.ContainsKey(data))
                                        {
                                            _images.Add(data, decoded);
                                            decoded = null;
                                        }
                                        foreach (var visible in matches) visible.ImageData = data;
                                        SynchronizeImages();
                                        InvalidateTiles();
                                    }
                                }
                                else _failedRequests.Add(tile);
                            }
                        }
                        catch (Exception ex) { Trace.TraceError("Publishing tile failed: {0}", ex); }
                        finally
                        {
                            decoded?.Dispose();
                            cancellation.Dispose();
                            try { PumpLoads(); }
                            catch (Exception ex) { Trace.TraceError("Scheduling tiles failed: {0}", ex); }
                        }
                    }, DispatcherPriority.Normal);
                }
                catch
                {
                    decoded?.Dispose();
                    cancellation.Dispose();
                    throw;
                }
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
