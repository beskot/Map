using System;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AxamlMapControl.Source;
using Xunit;

namespace AxamlMapControlTests
{
    public class MapZoomAnimatorTests
    {
        [AvaloniaFact]
        public void DisabledAnimationAppliesZoomWithoutCapturing()
        {
            var host = new Host();
            using var animator = new MapZoomAnimator(host);
            var focus = new Point(123, 234);
            animator.ZoomTo(5, focus, false, TimeSpan.FromMilliseconds(200));
            Assert.Equal(5, host.ZoomLevel);
            Assert.Equal(focus, host.Focus);
            Assert.Equal(0, host.Captures);
            Assert.False(animator.SuppressGraticuleRendering);
            Assert.False(animator.TryGetTileAnimation(out _, out _));
        }
        [AvaloniaFact]
        public void FailedCaptureResetsFlagsAndStillAppliesZoom()
        {
            var host = new Host();
            using var animator = new MapZoomAnimator(host);
            host.DuringCapture = () =>
            {
                Assert.True(animator.SuppressGraticuleRendering);
                Assert.True(animator.SuppressTileCapture);
            };
            animator.ZoomTo(3, new Point(20, 30), true, TimeSpan.FromMilliseconds(200));
            Assert.Equal(1, host.Captures);
            Assert.Equal(3, host.ZoomLevel);
            Assert.False(animator.SuppressGraticuleRendering);
            Assert.False(animator.SuppressTileCapture);
        }
        [AvaloniaFact]
        public void ZeroDurationDoesNotCaptureAndDisposedAnimatorRejectsZoom()
        {
            var host = new Host();
            var animator = new MapZoomAnimator(host);
            animator.ZoomTo(5, default, true, TimeSpan.Zero);
            Assert.Equal(0, host.Captures);
            animator.Stop();
            animator.Stop();
            animator.Dispose();
            animator.Dispose();
            Assert.Throws<ObjectDisposedException>(() => animator.ZoomTo(6, default, true, TimeSpan.FromMilliseconds(200)));
        }
        [AvaloniaFact]
        public void ExplicitFocusAndProgrammaticZoomBothUpdateMap()
        {
            var map = new Map { ZoomLevel = 4 };
            map.Measure(new Size(800, 600));
            map.Arrange(new Rect(0, 0, 800, 600));
            var focus = new Point(123, 234);
            var before = map.Transform.ViewToCartesian(focus);
            map.ZoomAt(5, focus);
            Close(before, map.Transform.ViewToCartesian(focus));
            var centerBefore = map.Transform.ViewToCartesian(new Point(400, 300));
            map.ZoomLevel = 6;
            Close(centerBefore, map.Transform.ViewToCartesian(new Point(400, 300)));
            Assert.Equal(6, map.ZoomLevel);
            Assert.False(map.TryGetTileAnimation(out _, out _));
        }
        private static void Close(Point expected, Point actual)
        {
            Assert.InRange(Math.Abs(expected.X - actual.X), 0d, 1e-6);
            Assert.InRange(Math.Abs(expected.Y - actual.Y), 0d, 1e-6);
        }
        private sealed class Host : IMapZoomAnimationHost
        {
            public int ZoomLevel { get; private set; } = 4;
            public Rect ViewBounds => new Rect(0, 0, 800, 600);
            public Point ViewCenter => new Point(400, 300);
            public bool HasView => true;
            public Point Focus { get; private set; }
            public int Captures { get; private set; }
            public Action? DuringCapture { get; set; }
            public RenderTargetBitmap CaptureSnapshot()
            {
                Captures++;
                DuringCapture?.Invoke();
                throw new InvalidOperationException("Simulated capture failure");
            }
            public void ApplyZoom(int target, Point focus) { ZoomLevel = target; Focus = focus; }
            public void AlignViewOrigin(Point origin) { }
            public Point ConstrainOrigin(Point origin, double zoom) => origin;
            public void InvalidateLayers() { }
            public void DrawOverlays(DrawingContext context, double scale, Point origin) { }
        }
    }
}
