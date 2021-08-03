using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AxamlMapControl.Source
{
    public class TileImageLoader : ITileImageLoader
    {
        private ITileSource? _source;
        private readonly ConcurrentQueue<Tile> _tileQueue;
        private int _taskCount;
        private int _taskLimit;

        public HttpClient HttpClient { get; private set; } = new() { Timeout = TimeSpan.FromSeconds(15) };

        public TileImageLoader()
        {
            _tileQueue = new();
            _taskCount = 0;
            _taskLimit = 4;
        }

        public async Task TileLoadAsync(IEnumerable<Tile> tiles, ITileSource source)
        {
            _source = source;
            _tileQueue.Clear();

            if (tiles.Any() && source != null)
            {
                foreach (var tile in tiles)
                {
                    _tileQueue.Enqueue(tile);
                }

                while (_taskCount < Math.Min(_tileQueue.Count, _taskLimit))
                {
                    Interlocked.Increment(ref _taskCount);
                    await Task.Run(LoadTile);
                }
            }
        }

        private async Task LoadTile()
        {
            while (_tileQueue.TryDequeue(out var tile))
            {
                try
                {
                    using (var responseMessage = await HttpClient.GetAsync(_source?.GetUri(tile), HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                    {
                        if (responseMessage.IsSuccessStatusCode)
                        {
                            tile.ImageData = await responseMessage.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Avalonia.Logging.Logger.TryGet(Avalonia.Logging.LogEventLevel.Debug, ex.Message);
                }
            }
            Interlocked.Decrement(ref _taskCount);
        }
    }
}