using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace AxamlMapControl.Source.Cache
{
    /// <summary>
    /// TileLoader caching in the memory.
    /// </summary>
    public class TileImageCache : ITileImageLoader
    {
        private ITileImageLoader _tileLoader;
        private IMemoryCache _cache;

        public TileImageCache()
        {
            _tileLoader = new TileImageLoader();
            _cache = new MemoryCache(new MemoryCacheOptions());
        }

        public async Task TileLoadAsync(IEnumerable<Tile> tiles, ITileSource source)
        {
            foreach (var tile in tiles)
            {
                tile.ImageData = _cache.Get<byte[]>(source.GetUri(tile).ToString());
            }

            if (tiles.Where(p => p.ImageData is null) is { } list && list.Any())
            {
                list = list.ToList();
                await _tileLoader.TileLoadAsync(list, source);
                await Task.Run(() =>
                {
                    foreach (var item in list)
                    {
                        _cache.Set(
                            source.GetUri(item).ToString(),
                            item.ImageData,
                            new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(10)));
                    }
                });
            }
        }
    }
}