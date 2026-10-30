using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using AxamlMapControl.Source.Proj;
using AxamlMapControl.Source.Shapes;
using Location = AxamlMapControl.Source.Proj.Location;

namespace AxamlMapControl.Source
{
    public class MapLocationLayer : MapBaseLayer
    {
        public static readonly StyledProperty<bool> ShowChildrenBoundsProperty =
            AvaloniaProperty.Register<MapLocationLayer, bool>(nameof(ShowChildrenBounds), false);
        public static readonly AttachedProperty<Location?> LocationProperty =
            AvaloniaProperty.RegisterAttached<MapLocationLayer, Control, Location?>("Location");
        private readonly LayerDrawingSurface _boundsSurface;

        static MapLocationLayer()
        {
            LocationProperty.Changed.AddClassHandler<Control>((control, _) =>
            {
                control.InvalidateArrange();
                if (control.GetVisualParent() is MapLocationLayer layer) layer.InvalidateArrange();
            });
            ShowChildrenBoundsProperty.Changed.AddClassHandler<MapLocationLayer>(
                (layer, _) => layer._boundsSurface?.InvalidateVisual());
        }

        public MapLocationLayer()
        {
            ClipToBounds = true;
            _boundsSurface = new LayerDrawingSurface(DrawChildrenBounds);
            // A visual-only overlay must not become an ItemsPresenter item/container.
            VisualChildren.Add(_boundsSurface);
        }

        public static void SetLocation(AvaloniaObject element, Location? location) =>
            element.SetValue(LocationProperty, location);
        public static Location? GetLocation(AvaloniaObject element) => element.GetValue(LocationProperty);

        public bool ShowChildrenBounds
        {
            get => GetValue(ShowChildrenBoundsProperty);
            set => SetValue(ShowChildrenBoundsProperty, value);
        }

        protected override void OnViewChanged(int zoomLevel) => InvalidateArrange();

        private void DrawChildrenBounds(DrawingContext context)
        {
            if (!ShowChildrenBounds) return;
            var pen = new Pen(Brushes.Gray, 2);
            var viewport = new Rect(Bounds.Size);
            foreach (var child in Children)
                if (child.Bounds.Intersects(viewport))
                    context.DrawRectangle(Brushes.Transparent, pen, child.Bounds);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var unconstrained = new Size(double.PositiveInfinity, double.PositiveInfinity);
            foreach (var element in Children) element.Measure(unconstrained);
            _boundsSurface.Measure(new Size());
            return new Size();
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (MapParent is { } map && map.Bounds.Width > 0 && map.Bounds.Height > 0)
            {
                foreach (var item in Children)
                {
                    if (GetLocation(item) is not { } location)
                    {
                        item.Arrange(new Rect(0, 0, 0, 0));
                        continue;
                    }
                    var position = map.Transform.LocationToView(location);
                    position = ElementPositionConstrain(item, position);
                    position = ElementAligment(item, position);
                    var target = new Rect(position, item.DesiredSize);
                    if (item is MapPath shape && IsOutsideViewport(target, finalSize, shape.StrokeThickness))
                    {
                        // Keep cached geometry and DesiredSize, without changing user visibility/bindings.
                        // Place beyond the clip even if minimum-size/alignment expands a zero-size slot.
                        var outside = new Point(finalSize.Width + item.DesiredSize.Width + 64,
                            finalSize.Height + item.DesiredSize.Height + 64);
                        item.Arrange(new Rect(outside, new Size()));
                        continue;
                    }
                    item.Arrange(target);
                }
            }
            _boundsSurface.Arrange(new Rect(finalSize));
            if (ShowChildrenBounds) _boundsSurface.InvalidateVisual();
            return finalSize;
        }

        private static bool IsOutsideViewport(Rect bounds, Size viewport, double strokeThickness)
        {
            if (!double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) ||
                !double.IsFinite(bounds.Right) || !double.IsFinite(bounds.Bottom)) return false;
            var margin = 32d + (double.IsFinite(strokeThickness) ? Math.Max(0, strokeThickness) * 2 : 0);
            // Inclusive comparisons retain zero-height/width lines crossing the screen.
            return bounds.Right < -margin || bounds.Bottom < -margin ||
                bounds.X > viewport.Width + margin || bounds.Y > viewport.Height + margin;
        }

        private Point ElementAligment(Control element, Point position)
        {
            var x = element.HorizontalAlignment switch
            {
                HorizontalAlignment.Center => position.X - element.DesiredSize.Width / 2,
                HorizontalAlignment.Right => position.X - element.DesiredSize.Width,
                _ => position.X
            };
            var y = element.VerticalAlignment switch
            {
                VerticalAlignment.Center => position.Y - element.DesiredSize.Height / 2,
                VerticalAlignment.Bottom => position.Y - element.DesiredSize.Height,
                _ => position.Y
            };
            return new Point(x, y);
        }

        private Point ElementPositionConstrain(Control element, Point position)
        {
            var constrain = MapParent.Transform.BoundsWidthConstrain(Bounds.Width);
            return position.WithX(ViewTransform.NormalizeValue(
                position.X + element.DesiredSize.Width, constrain) - element.DesiredSize.Width);
        }
    }
}
