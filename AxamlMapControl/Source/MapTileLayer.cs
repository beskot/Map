using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private AvaloniaObject? _observedSource;
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
                _tileList = new List<Tile>();
                _currentTileMatrix = MapTileMatrix.Empty;
                InvalidateVisual();
            };
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
            _tileList = new List<Tile>();
            _currentTileMatrix = MapTileMatrix.Empty;
            ObserveSource(MapParent is null ? null : TileSource as AvaloniaObject);
            InvalidateVisual();
            if (MapParent is { } map && !map.Bounds.IsEmpty) OnViewChanged(map.ZoomLevel);
        }

        public override void Render(DrawingContext context)
        {
            foreach (var tile in _tileList)
            {
                if (tile.ImageData is null) continue;
                context.DrawImage(new Bitmap(new MemoryStream(tile.ImageData)),
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
                _generation++;
            }

            RenderTransform = new MatrixTransform(Matrix.CreateTranslation(
                ViewTopLeft + _currentTileMatrix.Origin + PointTranslated));
            InvalidateVisual();
            if (!changed || source is null || loader is null) return;
            if (source is TileSource && string.IsNullOrWhiteSpace(source.UriStringFormat)) return;
            var pending = _tileList.Where(tile => tile.ImageData is null).ToList();
            if (pending.Count == 0) return;
            var snapshot = new ResolvedSource(source, pending);
            _ = LoadTilesAsync(loader, snapshot, pending, _generation);
        }

        private async Task LoadTilesAsync(ITileImageLoader loader, ITileSource source,
            List<Tile> tiles, long generation)
        {
            try
            {
                await loader.TileLoadAsync(tiles, source).ConfigureAwait(false);
                Dispatcher.UIThread.Post(() =>
                {
                    if (generation == _generation && MapParent is { }) InvalidateVisual();
                });
            }
            catch (Exception ex)
            {
                Trace.TraceError("Tile batch failed: {0}", ex);
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
