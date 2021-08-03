using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AxamlMapControl.Source.Proj;

namespace AxamlMapControl.Source
{
    public class MapLocationLayer : MapBaseLayer
    {
        public static readonly StyledProperty<bool> ShowChildrenBoundsProperty
            = AvaloniaProperty.Register<MapLocationLayer, bool>(nameof(ShowChildrenBounds), false);

        public static readonly AttachedProperty<Proj.Location> LocationProperty
           = AvaloniaProperty.RegisterAttached<MapLocationLayer, Proj.Location>("Location", typeof(MapLocationLayer));

        public static void SetLocation(AvaloniaObject avaloniaObject, Proj.Location location)
        {
            avaloniaObject.SetValue(LocationProperty, location);
        }

        public static Proj.Location GetLocation(AvaloniaObject avaloniaObject)
        {
            return avaloniaObject.GetValue(LocationProperty);
        }

        public bool ShowChildrenBounds
        {
            get => GetValue(ShowChildrenBoundsProperty);
            set => SetValue(ShowChildrenBoundsProperty, value);
        }

        protected override void OnViewChanged(int zoomLevel)
        {
            InvalidateArrange();
        }


        public MapLocationLayer()
        {

        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (ShowChildrenBounds)
            {
                var pen = new Pen
                {
                    Brush = Brushes.Gray,
                    Thickness = 2
                };

                foreach (var child in Children)
                {
                    context.DrawRectangle(Brushes.Transparent, pen, child.Bounds);
                }
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            availableSize = new Size(double.PositiveInfinity, double.PositiveInfinity);

            foreach (var element in Children)
            {
                element.Measure(availableSize);
            }

            return new Size();
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (MapParent != null && !MapParent.Bounds.IsEmpty)
            {
                foreach (var item in Children)
                {
                    if (item is AvaloniaObject avaloniaObj &&
                        GetLocation(avaloniaObj) is { } location)
                    {
                        var position = MapParent.Transform.LocationToView(location);

                        position = ElementPositionConstrain(item, position);
                        position = ElementAligment(item, position);
                        item.Arrange(new Rect(position, item.DesiredSize));                            
                    }
                }
            }

            return finalSize;
        }

        /// <summary>
        /// Aligment position of map element.
        /// </summary>
        /// <param name="Element">Element</param>
        /// <param name="position">Position</param>
        /// <returns>Position</returns>
        private Point ElementAligment(IControl element, Point position)
        {
            var posX = element.HorizontalAlignment switch
            {
                HorizontalAlignment.Center => position.X - element.DesiredSize.Width / 2,
                HorizontalAlignment.Right => position.X - element.DesiredSize.Width,
                _ => position.X
            };

            var posY = element.VerticalAlignment switch
            {
                VerticalAlignment.Center => position.Y - element.DesiredSize.Height / 2,
                VerticalAlignment.Bottom => position.Y - element.DesiredSize.Height,
                _ => position.Y
            };

            return new Point(posX, posY);
        }

        /// <summary>
        /// Constrain position of map element.
        /// </summary>
        /// <param name="element">Element</param>
        /// <param name="position">Position of element</param>
        /// <returns>Constrain position</returns>
        private Point ElementPositionConstrain(IControl element, Point position)
        {
            var constrain = MapParent.Transform.BoundsWidthConstrain(Bounds.Width);

            position = position.WithX(ViewTransform.NormalizeValue(position.X + element.DesiredSize.Width, constrain));
            position = position.WithX(position.X - element.DesiredSize.Width);

            return position;
        }
    }
}