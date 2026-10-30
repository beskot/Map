using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using AxamlMapControl.Source;
using Xunit;

namespace AxamlMapControlTests
{
    public class LayerDrawingSurfaceTests
    {
        [AvaloniaFact]
        public void TileLayerKeepsPanelContractAndUsesNonInteractiveSurface()
        {
            var layer = new MapTileLayer();
            Assert.IsAssignableFrom<Panel>(layer);
            var surface = Assert.IsType<LayerDrawingSurface>(Assert.Single(layer.Children));
            Assert.False(surface.IsHitTestVisible);
            Assert.False(surface.Focusable);
            layer.Measure(new Size(400, 300));
            layer.Arrange(new Rect(0, 0, 400, 300));
            Assert.Equal(layer.Bounds.Size, surface.Bounds.Size);
        }

        [AvaloniaFact]
        public void BoundsOverlayIsNotPartOfMarkerCollection()
        {
            var layer = new MapLocationLayer();
            var marker = new Border();
            layer.Children.Add(marker);
            var surface = Assert.Single(layer.GetVisualChildren().OfType<LayerDrawingSurface>());
            Assert.Single(layer.Children);
            Assert.Same(marker, layer.Children[0]);
            Assert.False(surface.IsHitTestVisible);
            layer.Children.Clear();
            Assert.Empty(layer.Children);
            Assert.Same(surface, Assert.Single(layer.GetVisualChildren()));
        }
    }
}
