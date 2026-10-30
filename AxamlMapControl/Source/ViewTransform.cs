using System;
using Avalonia;
using AxamlMapControl.Source.Proj;

namespace AxamlMapControl.Source
{
    public class ViewTransform
    {
        // Retained for source compatibility. Prefer SetZoom and Resize for bounds changes.
        public Rect _mapBounds;
        private int _zoomCurrent;
        private Point _viewCenter;

        public Point TopLeftCartesianOrigin { get; } =
            new(-MapProjection.OriginShift, MapProjection.OriginShift);
        public Point BottomRightCartesianOrigin { get; } =
            new(MapProjection.OriginShift, -MapProjection.OriginShift);
        public double Scale { get; private set; }
        public MapProjection Projection { get; } = new();

        // Screen position of the projected origin, not the geographic viewport center.
        public Point ViewCenter
        {
            get => _viewCenter;
            set
            {
                ValidatePoint(value);
                _viewCenter = value;
            }
        }

        public EventHandler<MapEventArgs>? ViewBoundsChanged;

        public ViewTransform()
        {
            Scale = GetScale(0);
            ViewCenter = new Point();
        }

        private static void ValidatePoint(Point point)
        {
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
                throw new ArgumentOutOfRangeException(nameof(point));
        }

        private static void ValidateBounds(Rect bounds)
        {
            if (!double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) ||
                !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height) ||
                !double.IsFinite(bounds.Right) || !double.IsFinite(bounds.Bottom) ||
                bounds.Width <= 0 || bounds.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(bounds));
        }

        public double GetScale(int zoomLevel)
        {
            if (zoomLevel < 0 || zoomLevel > 30)
                throw new ArgumentOutOfRangeException(nameof(zoomLevel));
            return (2 * MapProjection.OriginShift / MapTileLayer.TileSize) / Math.Pow(2, zoomLevel);
        }

        public Point ViewToCartesian(Point point)
        {
            ValidatePoint(point);
            var result = new Point((point.X - ViewCenter.X) * Scale,
                (ViewCenter.Y - point.Y) * Scale);
            ValidatePoint(result);
            return result;
        }

        public Point CartesianToView(Point point)
        {
            ValidatePoint(point);
            var result = new Point(point.X / Scale + ViewCenter.X,
                ViewCenter.Y - point.Y / Scale);
            ValidatePoint(result);
            return result;
        }

        public Rect GetViewBounds()
        {
            var topLeft = ViewToCartesian(_mapBounds.TopLeft);
            return new Rect((topLeft.X - TopLeftCartesianOrigin.X) / Scale,
                (TopLeftCartesianOrigin.Y - topLeft.Y) / Scale,
                _mapBounds.Width, _mapBounds.Height);
        }

        private void Apply(Point origin, double scale, int zoom, Rect bounds)
        {
            ValidatePoint(origin);
            var translation = zoom == _zoomCurrent ? origin - ViewCenter : new Vector();
            _mapBounds = bounds;
            Scale = scale;
            _zoomCurrent = zoom;
            ViewCenter = origin;
            ViewBoundsChanged?.Invoke(this, new MapEventArgs
            {
                ZoomLevel = zoom,
                Translate = translation
            });
        }

        /// <summary>Change zoom while keeping the projected point under the focus fixed.</summary>
        public void SetZoom(Point point, int zoom, Rect bounds)
        {
            ValidateBounds(bounds);
            var scale = GetScale(zoom);
            var anchor = ViewToCartesian(point);
            Apply(new Point(point.X - anchor.X / scale, point.Y + anchor.Y / scale),
                scale, zoom, bounds);
        }

        /// <summary>Place a geographic location at the viewport center.</summary>
        public void SetZoom(Location location, int zoom, Rect bounds)
        {
            ValidateBounds(bounds);
            CenterOnCartesian(Projection.ToCartesian(location), zoom, bounds);
        }

        private void CenterOnCartesian(Point point, int zoom, Rect bounds)
        {
            var scale = GetScale(zoom);
            Apply(new Point(bounds.Center.X - point.X / scale,
                bounds.Center.Y + point.Y / scale), scale, zoom, bounds);
        }

        /// <summary>Resize without discarding the current projected viewport center.</summary>
        public void Resize(Rect bounds)
        {
            ValidateBounds(bounds);
            var oldCenter = _mapBounds.Width > 0 && _mapBounds.Height > 0
                ? _mapBounds.Center : bounds.Center;
            var point = ViewToCartesian(oldCenter);
            CenterOnCartesian(point, _zoomCurrent, bounds);
        }

        public void Translate(Vector translate)
        {
            ValidatePoint(new Point(translate.X, translate.Y));
            ViewCenter += translate;
            ViewBoundsChanged?.Invoke(this, new MapEventArgs
            {
                Translate = translate,
                ZoomLevel = _zoomCurrent
            });
        }

        public static int NormalizeValue(int val, int step)
        {
            if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step));
            var remainder = val % step;
            return remainder < 0 ? remainder + step : remainder;
        }

        public static double NormalizeValue(double val, double step)
        {
            if (!double.IsFinite(val)) throw new ArgumentOutOfRangeException(nameof(val));
            if (!double.IsFinite(step) || step <= 0)
                throw new ArgumentOutOfRangeException(nameof(step));
            var remainder = val % step;
            var result = remainder < 0 ? remainder + step : remainder;
            return result >= step ? 0 : result;
        }

        public double BoundsWidthConstrain(double bounds)
        {
            if (!double.IsFinite(bounds) || bounds < 0)
                throw new ArgumentOutOfRangeException(nameof(bounds));
            var mapWidth = 2 * MapProjection.OriginShift / Scale;
            var result = Math.Max(1d, Math.Ceiling(bounds / mapWidth)) * mapWidth;
            if (!double.IsFinite(result)) throw new ArgumentOutOfRangeException(nameof(bounds));
            return result;
        }
    }
}
