using System;
using System.Collections.Generic;
using Avalonia;
using AxamlMapControl.Source;
using AxamlMapControl.Source.Proj;
using Xunit;

namespace AxamlMapControlTests
{
    public class ViewTransformTests
    {
        [Fact]
        public void FreshTransformIsInvertible()
        {
            var transform = new ViewTransform();
            var point = new Point(123, -45);
            Close(point, transform.CartesianToView(transform.ViewToCartesian(point)));
            Close(new Point(0, 0), transform.CartesianToView(new Point(0, 0)));
        }

        [Fact]
        public void LocationIsCenteredEvenWhenZoomDoesNotChange()
        {
            var transform = new ViewTransform();
            var bounds = new Rect(0, 0, 800, 600);
            transform.SetZoom(new Location(0, 0), 4, bounds);
            var location = new Location(52.37, 4.9);
            transform.SetZoom(location, 4, bounds);
            Close(bounds.Center, transform.LocationToView(location));
        }

        [Fact]
        public void ZoomPreservesProjectedPointUnderCursor()
        {
            var transform = new ViewTransform();
            var bounds = new Rect(0, 0, 800, 600);
            transform.SetZoom(new Location(52, 5), 4, bounds);
            var focus = new Point(123, 456);
            var before = transform.ViewToCartesian(focus);
            transform.SetZoom(focus, 7, bounds);
            Close(before, transform.ViewToCartesian(focus));
            transform.SetZoom(focus, 4, bounds);
            Close(before, transform.ViewToCartesian(focus));
        }

        [Fact]
        public void ResizeKeepsThePannedProjectedCenter()
        {
            var transform = new ViewTransform();
            var oldBounds = new Rect(0, 0, 800, 600);
            transform.SetZoom(new Location(52, 5), 5, oldBounds);
            transform.Translate(new Vector(75, -30));
            var before = transform.ViewToCartesian(oldBounds.Center);
            var nextBounds = new Rect(0, 0, 1200, 700);
            transform.Resize(nextBounds);
            Close(before, transform.ViewToCartesian(nextBounds.Center));
        }

        [Fact]
        public void ReentrantPanSeesTheNewZoom()
        {
            var transform = new ViewTransform();
            var zooms = new List<int>();
            transform.ViewBoundsChanged += (_, e) =>
            {
                zooms.Add(e.ZoomLevel);
                if (zooms.Count == 1) transform.Translate(new Vector(1, 0));
            };
            transform.SetZoom(new Location(0, 0), 6, new Rect(0, 0, 800, 600));
            Assert.Equal(new[] { 6, 6 }, zooms);
        }

        [Fact]
        public void SameZoomCenterChangeReportsTranslation()
        {
            var transform = new ViewTransform();
            var bounds = new Rect(0, 0, 800, 600);
            transform.SetZoom(new Location(0, 0), 4, bounds);
            var before = transform.ViewCenter;
            Vector reported = default;
            transform.ViewBoundsChanged += (_, e) => reported = e.Translate;
            transform.SetZoom(new Location(10, 20), 4, bounds);
            var expected = transform.ViewCenter - before;
            Close(new Point(expected.X, expected.Y), new Point(reported.X, reported.Y));
        }

        [Fact]
        public void OffsetBoundsProduceCorrectWorldPixelBounds()
        {
            var transform = new ViewTransform();
            var bounds = new Rect(100, 50, 256, 256);
            transform.SetZoom(new Location(0, 0), 0, bounds);
            var actual = transform.GetViewBounds();
            Close(new Point(0, 0), actual.TopLeft);
            Assert.Equal(bounds.Size, actual.Size);
        }

        [Fact]
        public void NormalizationHandlesLargeIntegersAndRejectsInvalidSteps()
        {
            Assert.Equal(int.MaxValue - 1, ViewTransform.NormalizeValue(-1, int.MaxValue));
            Assert.Equal(int.MaxValue - 1, ViewTransform.NormalizeValue(int.MinValue, int.MaxValue));
            Assert.Equal(7, ViewTransform.NormalizeValue(-1, 8));
            Assert.Equal(7d, ViewTransform.NormalizeValue(-1d, 8d));
            Assert.Throws<ArgumentOutOfRangeException>(() => ViewTransform.NormalizeValue(1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ViewTransform.NormalizeValue(1d, -1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => ViewTransform.NormalizeValue(double.NaN, 8d));
        }

        [Fact]
        public void ScaleAndWidthConstraintsValidateTheirInputs()
        {
            var transform = new ViewTransform();
            Assert.Equal(256d, transform.BoundsWidthConstrain(256));
            Assert.Throws<ArgumentOutOfRangeException>(() => transform.GetScale(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => transform.GetScale(31));
            Assert.Throws<ArgumentOutOfRangeException>(() => transform.BoundsWidthConstrain(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => transform.Translate(new Vector(double.NaN, 0)));
        }

        private static void Close(Point expected, Point actual)
        {
            Assert.InRange(Math.Abs(expected.X - actual.X), 0d, 1e-6);
            Assert.InRange(Math.Abs(expected.Y - actual.Y), 0d, 1e-6);
        }
    }
}
