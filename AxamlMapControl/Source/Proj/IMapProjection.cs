using Avalonia;

namespace AxamlMapControl.Source.Proj
{
    public interface IMapProjection
    {
        public Vector GetRelativeScale(Location location);
        public Location ToLocation(Point point);
        public Point ToCartesian(Location location);
    }
}