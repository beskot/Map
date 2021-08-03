using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AxamlMapControl.Source
{
    /// <summary>
    /// Map base panel. 
    /// </summary>
    public abstract class MapBaseLayer : Panel, IMapElement
    {
        private int _zoom;

        public Map MapParent { get; private set; } = default!;
        public Point PointTranslated { get; private set; }
        public Point ViewTopLeft { get; private set; }

        public MapBaseLayer()
        {
            AttachedToVisualTree += (o, e) =>
            {
                MapParent = this.FindAncestorOfType<Map>();
                MapParent.Transform.ViewBoundsChanged += MapViewBoundsChangedHandler;
            };
        }

        private void MapViewBoundsChangedHandler(object? sender, MapEventArgs e)
        {
            PointTranslated += e.Translate;

            if (_zoom != e.ZoomLevel)
            {
                _zoom = e.ZoomLevel;
                PointTranslated = new();
                ViewTopLeft = MapParent.Transform.CartesianToView(MapParent.Transform.TopLeftCartesianOrigin);
            }

            OnViewChanged(e.ZoomLevel);
        }

        protected abstract void OnViewChanged(int zoomLevel);
    }
}