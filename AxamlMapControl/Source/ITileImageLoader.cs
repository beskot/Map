using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AxamlMapControl.Source
{
    public interface ITileImageLoader
    {
        Task TileLoadAsync(IEnumerable<Tile> tiles, ITileSource source);

        // Compatibility fallback: legacy implementations cannot interrupt their work.
        async Task TileLoadAsync(IEnumerable<Tile> tiles, ITileSource source,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await TileLoadAsync(tiles, source).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
