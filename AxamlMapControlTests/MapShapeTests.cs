using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AxamlMapControl.Source;
using AxamlMapControl.Source.Proj;
using AxamlMapControl.Source.Shapes;
using Xunit;

namespace AxamlMapControlTests
{
    public class MapShapeTests
    {
        [Fact]
        public void LinesKeepIndependentEndpointsAndAnchors()
        {
            var first = new MapLine { StartPoint = new Location(10, 20), EndPoint = new Location(5, 30) };
            var second = new MapLine { StartPoint = new Location(-10, -20), EndPoint = new Location(-5, -30) };
            second.StartPoint = new Location(50, 60);
            Assert.Equal(new Location(10, 20), MapLocationLayer.GetLocation(first));
            Assert.Equal(new Location(10, 20), first.StartPoint);
            Assert.Equal(new Location(5, 30), first.EndPoint);
        }

        [Fact]
        public void IncompleteShapesHaveEmptyGeometry()
        {
            var map = new Map();
            var line = new MapLine();
            line.SetMapParent(map);
            Assert.Empty(((PathGeometry)line.Data).Figures);
            line.StartPoint = new Location(1, 2);
            Assert.Empty(((PathGeometry)line.Data).Figures);
            line.EndPoint = new Location(3, 4);
            Assert.Single(((PathGeometry)line.Data).Figures);
            line.EndPoint = null;
            Assert.Empty(((PathGeometry)line.Data).Figures);
            var polygon = new MapPolygon();
            polygon.SetMapParent(map);
            polygon.Locations.Add(new Location(0, 0));
            polygon.Locations.Add(new Location(1, 1));
            Assert.Empty(((PathGeometry)polygon.Data).Figures);
            polygon.Locations.Add(new Location(0, 1));
            Assert.Single(((PathGeometry)polygon.Data).Figures);
            polygon.Locations.Clear();
            Assert.Empty(((PathGeometry)polygon.Data).Figures);
            line.SetMapParent(null);
            polygon.SetMapParent(null);
        }

        [Fact]
        public void CollectionEditsUpdateAnchorAndOldCollectionIsUnsubscribed()
        {
            var old = new Locations(new Location(1, 2), new Location(3, 4));
            var line = new MapPolyline { Locations = old };
            Assert.Equal(new Location(3, 2), MapLocationLayer.GetLocation(line));
            old[0] = new Location(8, -5);
            Assert.Equal(new Location(8, -5), MapLocationLayer.GetLocation(line));
            line.Locations = new Locations(new Location(10, 11), new Location(12, 13));
            old.Add(new Location(80, -170));
            Assert.Equal(new Location(12, 11), MapLocationLayer.GetLocation(line));
            line.Locations.Clear();
            Assert.Null(MapLocationLayer.GetLocation(line));
            line.Locations = null!;
            Assert.Empty(((PathGeometry)line.Data).Figures);
        }

        [Fact]
        public void PathSubscriptionsFollowOnlyTheCurrentMap()
        {
            var first = new Map();
            var second = new Map();
            var path = new CountingPath();
            path.SetMapParent(first);
            path.SetMapParent(first);
            Assert.Equal(1, path.Updates);
            first.Transform.Translate(new Vector(1, 0));
            Assert.Equal(2, path.Updates);
            path.SetMapParent(second);
            Assert.Equal(3, path.Updates);
            first.Transform.Translate(new Vector(1, 0));
            Assert.Equal(3, path.Updates);
            path.SetMapParent(null);
            second.Transform.Translate(new Vector(1, 0));
            Assert.Equal(3, path.Updates);
        }

        [Fact]
        public void ZoomRebuildsLineGeometryWithoutWaitingForArrange()
        {
            var map = new Map();
            var line = new MapLine { StartPoint = new Location(10, 0), EndPoint = new Location(0, 10) };
            line.SetMapParent(map);
            var initial = ((LineSegment)((PathGeometry)line.Data).Figures[0].Segments[0]).Point;
            map.Transform.SetZoom(new Location(0, 0), 2, new Rect(0, 0, 400, 300));
            var next = ((LineSegment)((PathGeometry)line.Data).Figures[0].Segments[0]).Point;
            Assert.InRange(Math.Abs(next.X - initial.X * 4), 0d, 1e-6);
            Assert.InRange(Math.Abs(next.Y - initial.Y * 4), 0d, 1e-6);
            line.SetMapParent(null);
        }

        [Fact]
        public void LocationChangeInvalidatesLayerArrangement()
        {
            var layer = new MapLocationLayer();
            var marker = new Border();
            layer.Children.Add(marker);
            MapLocationLayer.SetLocation(marker, new Location(0, 0));
            layer.Measure(new Size(400, 300));
            layer.Arrange(new Rect(0, 0, 400, 300));
            Assert.True(layer.IsArrangeValid);
            MapLocationLayer.SetLocation(marker, new Location(1, 2));
            Assert.False(layer.IsArrangeValid);
        }

        [Fact]
        public void EmptyFigureAndMissingPathDataDoNotThrow()
        {
            var path = new CountingPath();
            Assert.NotNull(path.EmptyFigure());
            path.Data = null!;
            path.SetMapParent(new Map());
            path.RefreshGeometry();
            path.SetMapParent(null);
        }

        private sealed class CountingPath : MapPath
        {
            public int Updates { get; private set; }
            protected override void Update()
            {
                if (MapParent is not null) Updates++;
                base.Update();
            }
            public PathFigure EmptyFigure() => CreateFigure(Array.Empty<Point>(), false, false);
        }
    }
}
