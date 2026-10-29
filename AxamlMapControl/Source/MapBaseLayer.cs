using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AxamlMapControl.Source
{
    public abstract class MapBaseLayer : Panel, IMapElement
    {
        private int _zoom = -1;
        public Map MapParent { get; private set; } = default!;
        public Point PointTranslated { get; private set; }
        public Point ViewTopLeft { get; private set; }

        public MapBaseLayer()
        {
            AttachedToVisualTree += (_, _) =>
            {
                Unsubscribe();
                MapParent = this.FindAncestorOfType<Map>();
                if (MapParent is null) return;
                _zoom = -1;
                PointTranslated = new Point();
                MapParent.Transform.ViewBoundsChanged += MapViewBoundsChangedHandler;
                if (!MapParent.Bounds.IsEmpty)
                    MapViewBoundsChangedHandler(MapParent.Transform,
                        new MapEventArgs { ZoomLevel = MapParent.ZoomLevel });
            };
            DetachedFromVisualTree += (_, _) => Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (MapParent is { } map)
                map.Transform.ViewBoundsChanged -= MapViewBoundsChangedHandler;
            MapParent = default!;
        }

        private void MapViewBoundsChangedHandler(object? sender, MapEventArgs e)
        {
            if (MapParent is null) return;
            PointTranslated += e.Translate;
            if (_zoom != e.ZoomLevel)
            {
                _zoom = e.ZoomLevel;
                PointTranslated = new Point();
                ViewTopLeft = MapParent.Transform.CartesianToView(
                    MapParent.Transform.TopLeftCartesianOrigin);
            }
            OnViewChanged(e.ZoomLevel);
        }

        protected abstract void OnViewChanged(int zoomLevel);
    }
}
