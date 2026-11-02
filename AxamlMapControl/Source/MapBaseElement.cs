using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AxamlMapControl.Source
{
    /// <summary>Map attachment and view-change lifecycle shared by overlays.</summary>
    public abstract class MapBaseElement : Control, IMapElement
    {
        public Map MapParent { get; private set; } = default!;
        public ViewTransform Transform => MapParent.Transform;

        protected MapBaseElement()
        {
            AttachedToVisualTree += (_, _) => SetMapParent(this.FindAncestorOfType<Map>());
            DetachedFromVisualTree += (_, _) => SetMapParent(null);
        }

        internal void SetMapParent(Map? map)
        {
            if (ReferenceEquals(MapParent, map)) return;
            var previous = MapParent;
            if (previous is { })
                previous.Transform.ViewBoundsChanged -= MapViewBoundsChangedHandler;
            MapParent = map!;
            OnMapParentChanged(previous, map);
            if (map is { })
            {
                map.Transform.ViewBoundsChanged += MapViewBoundsChangedHandler;
                if (map.Bounds.Width > 0 && map.Bounds.Height > 0) OnViewChanged(map.ZoomLevel);
            }
            InvalidateVisual();
        }

        protected virtual void OnMapParentChanged(Map? previous, Map? current) { }

        private void MapViewBoundsChangedHandler(object? sender, MapEventArgs e)
        {
            if (MapParent is { } map && ReferenceEquals(sender, map.Transform))
                OnViewChanged(e.ZoomLevel);
        }

        protected abstract void OnViewChanged(int zoomLevel);
    }
}
