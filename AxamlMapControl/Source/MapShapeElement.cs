using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;

namespace AxamlMapControl.Source
{
    /// <summary>Base for custom-drawn map geometry, independent of text styling.</summary>
    public abstract class MapShapeElement : MapBaseElement
    {
        public static readonly StyledProperty<IBrush?> FillProperty =
            AvaloniaProperty.Register<MapShapeElement, IBrush?>(nameof(Fill));
        public static readonly StyledProperty<Stretch> StretchProperty =
            AvaloniaProperty.Register<MapShapeElement, Stretch>(nameof(Stretch));
        public static readonly StyledProperty<IBrush?> StrokeProperty =
            AvaloniaProperty.Register<MapShapeElement, IBrush?>(nameof(Stroke));
        public static readonly StyledProperty<AvaloniaList<double>?> StrokeDashArrayProperty =
            AvaloniaProperty.Register<MapShapeElement, AvaloniaList<double>?>(nameof(StrokeDashArray));
        public static readonly StyledProperty<double> StrokeDashOffsetProperty =
            AvaloniaProperty.Register<MapShapeElement, double>(nameof(StrokeDashOffset));
        public static readonly StyledProperty<double> StrokeThicknessProperty =
            AvaloniaProperty.Register<MapShapeElement, double>(nameof(StrokeThickness));
        public static readonly StyledProperty<PenLineCap> StrokeLineCapProperty =
            AvaloniaProperty.Register<MapShapeElement, PenLineCap>(nameof(StrokeLineCap), PenLineCap.Flat);
        public static readonly StyledProperty<PenLineJoin> StrokeJoinProperty =
            AvaloniaProperty.Register<MapShapeElement, PenLineJoin>(nameof(StrokeJoin), PenLineJoin.Miter);

        public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
        public Stretch Stretch { get => GetValue(StretchProperty); set => SetValue(StretchProperty, value); }
        public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
        public AvaloniaList<double>? StrokeDashArray { get => GetValue(StrokeDashArrayProperty); set => SetValue(StrokeDashArrayProperty, value); }
        public double StrokeDashOffset { get => GetValue(StrokeDashOffsetProperty); set => SetValue(StrokeDashOffsetProperty, value); }
        public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
        public PenLineCap StrokeLineCap { get => GetValue(StrokeLineCapProperty); set => SetValue(StrokeLineCapProperty, value); }
        public PenLineJoin StrokeJoin { get => GetValue(StrokeJoinProperty); set => SetValue(StrokeJoinProperty, value); }

        protected IPen Pen { get; private set; } = new Pen(Brushes.Transparent, 0);
        private AvaloniaList<double>? _observedDashes;

        static MapShapeElement()
        {
            Observable.Merge<AvaloniaPropertyChangedEventArgs>(
                StrokeProperty.Changed, StrokeThicknessProperty.Changed,
                StrokeDashOffsetProperty.Changed, StrokeLineCapProperty.Changed,
                StrokeJoinProperty.Changed)
                .AddClassHandler<MapShapeElement>((element, _) => element.InvalidatePen());
            StrokeDashArrayProperty.Changed.AddClassHandler<MapShapeElement>((element, _) =>
            {
                element.ObserveDashes(element.MapParent is null ? null : element.StrokeDashArray);
                element.InvalidatePen();
            });
            Observable.Merge<AvaloniaPropertyChangedEventArgs>(FillProperty.Changed, StretchProperty.Changed)
                .AddClassHandler<MapShapeElement>((element, _) => element.InvalidateVisual());
        }

        protected MapShapeElement() => InvalidatePen();

        protected override void OnMapParentChanged(Map? previous, Map? current)
        {
            base.OnMapParentChanged(previous, current);
            ObserveDashes(current is null ? null : StrokeDashArray);
            if (current is { }) InvalidatePen();
        }

        private void ObserveDashes(AvaloniaList<double>? dashes)
        {
            if (ReferenceEquals(_observedDashes, dashes)) return;
            if (_observedDashes is { } previous) previous.CollectionChanged -= DashesChanged;
            _observedDashes = dashes;
            if (dashes is { }) dashes.CollectionChanged += DashesChanged;
        }

        private void DashesChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidatePen();

        private void InvalidatePen()
        {
            var dashes = StrokeDashArray;
            DashStyle? dashStyle = dashes is { Count: > 0 }
                ? new DashStyle(dashes.ToArray(), StrokeDashOffset) : null;
            Pen = new Pen(Stroke, StrokeThickness, dashStyle, StrokeLineCap, StrokeJoin);
            InvalidateVisual();
        }
    }
}
