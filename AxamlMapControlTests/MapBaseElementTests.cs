using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;
using AxamlMapControl.Source;
using Xunit;

namespace AxamlMapControlTests
{
    public class MapBaseElementTests
    {
        [Fact]
        public void SwitchingParentsAndDetachingRemoveOldSubscriptions()
        {
            var first = new Map();
            var second = new Map();
            var element = new TestElement();
            element.SetMapParent(first);
            element.SetMapParent(first);
            first.Transform.Translate(new Vector(1, 0));
            Assert.Equal(1, element.Updates);
            element.SetMapParent(second);
            first.Transform.Translate(new Vector(1, 0));
            Assert.Equal(1, element.Updates);
            second.Transform.Translate(new Vector(1, 0));
            Assert.Equal(2, element.Updates);
            element.SetMapParent(null);
            second.Transform.Translate(new Vector(1, 0));
            Assert.Equal(2, element.Updates);
            Assert.Null(element.MapParent);
        }

        [Fact]
        public void AttachingToAnArrangedMapPerformsInitialUpdate()
        {
            var map = new Map();
            map.Measure(new Size(400, 300));
            map.Arrange(new Rect(0, 0, 400, 300));
            var element = new TestElement();
            element.SetMapParent(map);
            Assert.Equal(1, element.Updates);
            element.SetMapParent(null);
        }

        [Fact]
        public void DashOffsetAndCollectionMutationsRebuildPen()
        {
            var dashes = new AvaloniaList<double> { 2, 3 };
            var element = new TestElement { Stroke = Brushes.Black, StrokeDashArray = dashes };
            element.SetMapParent(new Map());
            var initial = element.CurrentPen;
            element.StrokeDashOffset = 4;
            Assert.NotSame(initial, element.CurrentPen);
            Assert.Equal(4d, element.CurrentPen.DashStyle!.Offset);
            var offsetPen = element.CurrentPen;
            dashes[0] = 5;
            Assert.NotSame(offsetPen, element.CurrentPen);
            Assert.Equal(new double[] { 5, 3 }, element.CurrentPen.DashStyle!.Dashes);
            Assert.Equal(new double[] { 2, 3 }, offsetPen.DashStyle!.Dashes);
            var replacement = new AvaloniaList<double> { 1, 1 };
            element.StrokeDashArray = replacement;
            var replacedPen = element.CurrentPen;
            dashes.Add(9);
            Assert.Same(replacedPen, element.CurrentPen);
            element.SetMapParent(null);
            replacement.Add(7);
            Assert.Same(replacedPen, element.CurrentPen);
            element.SetMapParent(new Map());
            Assert.Equal(new double[] { 1, 1, 7 }, element.CurrentPen.DashStyle!.Dashes);
            element.SetMapParent(null);
        }

        private sealed class TestElement : MapBaseElement
        {
            public int Updates { get; private set; }
            public IPen CurrentPen => Pen;
            protected override void OnViewChanged(int zoomLevel) => Updates++;
        }
    }
}
