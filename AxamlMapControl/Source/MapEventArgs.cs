using Avalonia;

namespace AxamlMapControl.Source
{
    public class MapEventArgs
    {
        public Vector Translate { get; set; }
        public int ZoomLevel { get; set; }
        public MapEventArgs()
        {
            Translate = Vector.Zero;
        }
    }
}