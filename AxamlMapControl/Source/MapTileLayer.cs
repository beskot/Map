using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AxamlMapControl.Source.Cache;

namespace AxamlMapControl.Source
{
    /// <summary>
    /// MapTileLayer.
    /// </summary>
    public class MapTileLayer : MapBaseLayer
    {

        public readonly StyledProperty<ITileImageLoader> TileImageLoaderProperty
            = AvaloniaProperty.Register<MapTileLayer, ITileImageLoader>(nameof(TileImageLoader),
            new TileImageCache());

        public readonly StyledProperty<ITileSource> TileSourceProperty
            = AvaloniaProperty.Register<MapTileLayer, ITileSource>(nameof(TileSource),
            new TileSource());

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

        private MapTileMatrix _currentTileMatrix;
        private List<Tile> _tileList;

        static MapTileLayer()
        {
            BackgroundProperty.OverrideDefaultValue<MapTileLayer>(Brushes.Transparent);
        }

        public MapTileLayer()
        {
            _currentTileMatrix = MapTileMatrix.Empty;
            _tileList = new();
            TileSourceProperty.Changed.AddClassHandler<MapTileLayer>((_, e) => TileSourceChanged(e));
        }

        private void TileSourceChanged(AvaloniaPropertyChangedEventArgs e)
        {
            //TODO Add handler TileSourceChanged
        }

        public override void Render(DrawingContext context)
        {
            foreach (var tile in _tileList)
            {
                if (tile is { } && tile.ImageData is { })
                {
                    context.DrawImage(new Bitmap(new MemoryStream(tile.ImageData)),
                    new Rect((tile.XIndex - _currentTileMatrix.Xmin) * TileSize,
                        (tile.Y - _currentTileMatrix.Ymin) * TileSize,
                        TileSize,
                        TileSize));
                }
            }
        }

        /// <summary>
        /// Update of the map tiles.
        /// </summary>
        private void UpdateTiles()
        {
            var old = _tileList.Where(p => p.Zoom == _currentTileMatrix.Zoom).ToList();

            _tileList.Clear();

            _currentTileMatrix.TraverseСells((Zoom, X, TileY) =>
            {
                var tileX = ViewTransform.NormalizeValue(X, _currentTileMatrix.NumTiles);

                _tileList.Add(old.Find(p =>
                    p.Zoom == Zoom &&
                    p.X == tileX &&
                    p.Y == TileY &&
                    p.XIndex == X) is { } tile
                        ? tile
                        : new Tile(Zoom, tileX, TileY) { XIndex = X });
            });
        }

        protected async override void OnViewChanged(int zoomLevel)
        {
            var viewBounds = MapParent.Transform.GetViewBounds();
            var tileMatrix = MapTileMatrix.Create(zoomLevel, viewBounds, TileSize);

            if (tileMatrix != _currentTileMatrix || tileMatrix.Length != _currentTileMatrix.Length)
            {
                _currentTileMatrix = tileMatrix;
                UpdateTiles();
                await TileImageLoader.TileLoadAsync(_tileList.Where(p => p.ImageData is null).ToList(), TileSource);
            }

            //When RenderTransform changed call InvalidateVisual(Render())
            RenderTransform = new MatrixTransform(Matrix.CreateTranslation(ViewTopLeft + _currentTileMatrix.Origin + PointTranslated));
        }
    }
}