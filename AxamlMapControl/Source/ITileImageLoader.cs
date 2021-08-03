using System.Collections.Generic;
using System.Threading.Tasks;

namespace AxamlMapControl.Source
{
    /// <summary>
    /// Loader map tile from repository.
    /// </summary>
    public interface ITileImageLoader
    {
        /// <summary>
        /// Load ImageData of current collection tiles
        /// </summary>
        /// <param name="tiles">Collestion tiles</param>
        /// <param name="source">Source data</param>
        /// <returns>Task</returns>
        Task TileLoadAsync(IEnumerable<Tile> tiles, ITileSource source);
    }
}