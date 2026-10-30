using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using AxamlMapControl.Source.Proj;
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
            foreach (var child in Children) context.DrawRectangle(Brushes.Transparent, pen, child.Bounds);
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
            if (MapParent is { } map && !map.Bounds.IsEmpty)
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
                    item.Arrange(new Rect(position, item.DesiredSize));
                }
            }
            _boundsSurface.Arrange(new Rect(finalSize));
            _boundsSurface.InvalidateVisual();
            return finalSize;
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
