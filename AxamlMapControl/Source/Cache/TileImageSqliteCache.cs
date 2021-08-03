using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;

namespace AxamlMapControl.Source.Cache
{
    /// <summary>
    /// TileLoader caching in the sqlite.
    /// </summary>
    public class TileImageSqliteCache : AvaloniaObject, ITileImageLoader, IDisposable
    {
        private TileContext _db;
        private object _dbLocker = new();

        public ITileImageLoader TileImageLoader { get; private set; }

        public static readonly StyledProperty<string> DataSourceProperty
            = AvaloniaProperty.Register<TileImageSqliteCache, string>(nameof(DataSource), string.Empty);

        public string DataSource
        {
            get => GetValue(DataSourceProperty);
            set => SetValue(DataSourceProperty, value);
        }

        public TileImageSqliteCache()
        {
            _db = new();
            TileImageLoader = new TileImageLoader();
            DataSourceProperty.Changed.AddClassHandler<TileImageSqliteCache>((_, e) => DataSourceChanged(e));
        }

        private void DataSourceChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is string fileName)
            {
                _db = File.Exists(fileName) ? new(fileName) : new();
            }
        }

        private Tile? TileFind(Tile tile)
        {
            lock (_dbLocker)
            {
                return _db.Tiles.Find(tile.Zoom, tile.X, tile.Y);
            }
        }

        private void WriteData(Tile tile)
        {
            lock (_dbLocker)
            {
                if (TileFind(tile) is null)
                {
                    _db.Tiles.Add(tile);
                    _db.SaveChangesAsync();
                }
            }
        }

        public void Dispose()
        {
            _db.Dispose();
        }

        public async Task TileLoadAsync(IEnumerable<Tile> tiles, ITileSource source)
        {
            foreach (var tile in tiles)
            {
                tile.ImageData = TileFind(tile)?.ImageData;
            }

            if (tiles.Where(p => p.ImageData is null) is { } list && list.Any())
            {
                list = list.ToList();
                await TileImageLoader.TileLoadAsync(list, source);
                await Task.Run(() =>
                {
                    foreach (var item in list)
                    {
                        WriteData(item);
                    }
                });
            }
        }
    }
}