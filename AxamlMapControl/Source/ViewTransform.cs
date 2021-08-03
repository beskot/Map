using System;
using Avalonia;
using AxamlMapControl.Source.Proj;

namespace AxamlMapControl.Source
{
    /// <summary>
    /// Map transform.
    /// </summary>
    public class ViewTransform
    {
        /// <summary>
        /// Map bounds
        /// </summary>
        public Rect _mapBounds;

        /// <summary>
        /// Zoom level
        /// </summary>
        private int _zoomCurrent;

        /// <summary>
        /// View center
        /// </summary>
        private Point _viewCenter;

        /// <summary>
        /// Matrix transform
        /// </summary>
        private Matrix _matrixCartesianTransform;

        /// <summary>
        /// Cartesian coordinate of map top left point
        /// </summary>
        public Point TopLeftCartesianOrigin { get; private set; }

        /// <summary>
        /// Cartesian coordinate of map bottom right point
        /// </summary>
        public Point BottomRightCartesianOrigin { get; private set; }

        /// <summary>
        /// Scale of map
        /// </summary>
        public double Scale { get; private set; }

        /// <summary>
        /// Map projection
        /// </summary>
        /// <value></value>
        public MapProjection Projection { get; }

        /// <summary>
        /// View center. 
        /// </summary>
        public Point ViewCenter
        {
            get => _viewCenter;
            set
            {
                _viewCenter = value;

                _matrixCartesianTransform = new Matrix(
                    Scale,
                    0,
                    0,
                    -Scale,
                    _viewCenter.X * -Scale,
                    _viewCenter.Y * Scale);
            }
        }

        /// <summary>
        /// Event handler
        /// </summary>
        public EventHandler<MapEventArgs>? ViewBoundsChanged;

        public ViewTransform()
        {
            _matrixCartesianTransform = new();
            Projection = new();

            TopLeftCartesianOrigin = new(-MapProjection.OriginShift, MapProjection.OriginShift);
            BottomRightCartesianOrigin = new(MapProjection.OriginShift, -MapProjection.OriginShift);

            Scale = GetScale(0);
        }

        /// <summary>
        /// Get scale.
        /// </summary>
        /// <param name="zoomLevel">Zoom</param>
        /// <returns>Scale</returns>
        public double GetScale(int zoomLevel)
        {
            if (zoomLevel < 0)
            {
                zoomLevel = 0;
            }

            var initialResolution = 2 * MapProjection.OriginShift / MapTileLayer.TileSize;
            var res = initialResolution / Math.Pow(2, zoomLevel);

            return res;
        }

        /// <summary>
        /// Get cartesian coordinate from display coordinate.
        /// </summary>
        /// <param name="point">Display coordinate</param>
        /// <returns>Cartesian coordinate</returns>
        public Point ViewToCartesian(Point point)
        {
            return point.Transform(_matrixCartesianTransform);
        }

        /// <summary>
        /// Get display coordinate from cartesian coordinate.
        /// </summary>
        /// <param name="point">Cartesian coordinate</param>
        /// <returns>Display coordinate</returns>
        public Point CartesianToView(Point point)
        {
            return point.Transform(_matrixCartesianTransform.Invert());
        }

        /// <summary>
        /// Transform of map bounds.
        /// </summary>
        /// <returns>Map bounds transformed</returns>
        public Rect GetViewBounds()
        {
            var viewTopLeft = _mapBounds.TopLeft
                .Transform(_matrixCartesianTransform);

            var point = new Point(
                (viewTopLeft.X - TopLeftCartesianOrigin.X) / Scale,
                (TopLeftCartesianOrigin.Y - viewTopLeft.Y) / Scale
            );

            return _mapBounds.Translate(point);
        }

        /// <summary>
        /// Calculate point of zoom.
        /// </summary>
        /// <param name="pointZoomFocus">Point of focus</param>
        /// <param name="newScale">Scale value</param>
        /// <returns></returns>
        private Point CalculateZoomPoint(Point pointZoomFocus, double newScale)
        {
            var viewOffsetX = (pointZoomFocus.X - ViewCenter.X) * Scale;
            var viewOffsetY = (pointZoomFocus.Y - ViewCenter.Y) * Scale;
            var contentOffsetX = viewOffsetX / newScale;
            var contentOffsetY = viewOffsetY / newScale;
            var newOffsetX = pointZoomFocus.X - contentOffsetX;
            var newOffsetY = pointZoomFocus.Y - contentOffsetY;

            Scale = newScale;

            return new(newOffsetX, newOffsetY);
        }

        /// <summary>
        /// Set zoom in display point.
        /// </summary>
        /// <param name="point">Display point</param>
        /// <param name="zoom">Zoom level</param>
        /// <param name="bounds">Map bounds</param>
        public void SetZoom(Point point, int zoom, Rect bounds)
        {
            _mapBounds = bounds;
            ViewCenter = CalculateZoomPoint(point, GetScale(zoom));

            ViewBoundsChanged?.Invoke(this, new MapEventArgs
            {
                ZoomLevel = zoom
            });

            _zoomCurrent = zoom;
        }

        /// <summary>
        /// Set zoom in location.
        /// </summary>
        /// <param name="location">Location</param>
        /// <param name="zoom">Zoom level</param>
        /// <param name="bounds">Map bounds</param>
        public void SetZoom(Location location, int zoom, Rect bounds)
        {
            if (ViewCenter.IsDefault)
            {
                ViewCenter = bounds.Center;
            }

            var point = CartesianToView(Projection.ToCartesian(location));
            SetZoom(point, zoom, bounds);
        }

        /// <summary>
        /// Translate of map.
        /// </summary>
        /// <param name="translate">Translate value</param>
        public void Translate(Vector translate)
        {
            ViewCenter += translate;

            ViewBoundsChanged?.Invoke(this, new MapEventArgs
            {
                Translate = translate,
                ZoomLevel = _zoomCurrent
            });
        }

        /// <summary>
        /// Get normalized value.
        /// </summary>
        /// <param name="val">Value</param>
        /// <param name="step">Step</param>
        /// <returns>Normal value</returns>
        public static int NormalizeValue(int val, int step)
        {
            return ((val % step) + step) % step;
        }

        /// <summary>
        /// Get normalized value.
        /// </summary>
        /// <param name="val">Value</param>
        /// <param name="step">Step</param>
        /// <returns>Normal value</returns>
        public static double NormalizeValue(double val, double step)
        {
            return ((val % step) + step) % step;
        }

        /// <summary>
        /// Constrain width of map bounds.
        /// </summary>
        /// <param name="bounds">Width of map bounds</param>
        /// <returns>Constrain width of map bounds</returns>
        public double BoundsWidthConstrain(double bounds)
        {
            var mapWidth = 2 * MapProjection.OriginShift / Scale;
            var viewWidth = (Math.Floor(bounds / mapWidth) + 1) * mapWidth;
            var constrain = Math.Max(mapWidth, viewWidth);

            return constrain;
        }
    }
}