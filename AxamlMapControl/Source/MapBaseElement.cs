using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AxamlMapControl.Source
{
    public abstract class MapBaseElement : Control, IMapElement
    {
        public static readonly StyledProperty<FontFamily> FontFamilyProperty =
            AvaloniaProperty.Register<MapBaseElement, FontFamily>(nameof(FontFamily), FontFamily.Default);
        public static readonly StyledProperty<double> FontSizeProperty =
            AvaloniaProperty.Register<MapBaseElement, double>(nameof(FontSize), 12);
        public static readonly StyledProperty<FontStyle> FontStyleProperty =
            AvaloniaProperty.Register<MapBaseElement, FontStyle>(nameof(FontStyle));
        public static readonly StyledProperty<FontWeight> FontWeightProperty =
            AvaloniaProperty.Register<MapBaseElement, FontWeight>(nameof(FontWeight), FontWeight.Normal);
        public static readonly StyledProperty<IBrush> ForegroundProperty =
            AvaloniaProperty.Register<MapBaseElement, IBrush>(nameof(Foreground), Brushes.Black);
        public static readonly StyledProperty<IBrush?> FillProperty =
            AvaloniaProperty.Register<MapBaseElement, IBrush?>(nameof(Fill));
        public static readonly StyledProperty<Stretch> StretchProperty =
            AvaloniaProperty.Register<MapBaseElement, Stretch>(nameof(Stretch));
        public static readonly StyledProperty<IBrush?> StrokeProperty =
            AvaloniaProperty.Register<MapBaseElement, IBrush?>(nameof(Stroke));
        public static readonly StyledProperty<AvaloniaList<double>?> StrokeDashArrayProperty =
            AvaloniaProperty.Register<MapBaseElement, AvaloniaList<double>?>(nameof(StrokeDashArray));
        public static readonly StyledProperty<double> StrokeDashOffsetProperty =
            AvaloniaProperty.Register<MapBaseElement, double>(nameof(StrokeDashOffset));
        public static readonly StyledProperty<double> StrokeThicknessProperty =
            AvaloniaProperty.Register<MapBaseElement, double>(nameof(StrokeThickness));
        public static readonly StyledProperty<PenLineCap> StrokeLineCapProperty =
            AvaloniaProperty.Register<MapBaseElement, PenLineCap>(nameof(StrokeLineCap), PenLineCap.Flat);
        public static readonly StyledProperty<PenLineJoin> StrokeJoinProperty =
            AvaloniaProperty.Register<MapBaseElement, PenLineJoin>(nameof(StrokeJoin), PenLineJoin.Miter);

        public FontFamily FontFamily
        {
            get => GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }
        public double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }
        public FontStyle FontStyle
        {
            get => GetValue(FontStyleProperty);
            set => SetValue(FontStyleProperty, value);
        }
        public FontWeight FontWeight
        {
            get => GetValue(FontWeightProperty);
            set => SetValue(FontWeightProperty, value);
        }
        public IBrush Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }
        public IBrush? Fill
        {
            get => GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }
        public Stretch Stretch
        {
            get => GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }
        public IBrush? Stroke
        {
            get => GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }
        public AvaloniaList<double>? StrokeDashArray
        {
            get => GetValue(StrokeDashArrayProperty);
            set => SetValue(StrokeDashArrayProperty, value);
        }
        public double StrokeDashOffset
        {
            get => GetValue(StrokeDashOffsetProperty);
            set => SetValue(StrokeDashOffsetProperty, value);
        }
        public double StrokeThickness
        {
            get => GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }
        public PenLineCap StrokeLineCap
        {
            get => GetValue(StrokeLineCapProperty);
            set => SetValue(StrokeLineCapProperty, value);
        }
        public PenLineJoin StrokeJoin
        {
            get => GetValue(StrokeJoinProperty);
            set => SetValue(StrokeJoinProperty, value);
        }

        public Map MapParent { get; private set; } = default!;
        protected IPen Pen { get; private set; } = new Pen(Brushes.Transparent, 0);
        public ViewTransform Transform => MapParent.Transform;
        private AvaloniaList<double>? _observedDashes;

        static MapBaseElement()
        {
            Observable.Merge<AvaloniaPropertyChangedEventArgs>(
                StrokeProperty.Changed, StrokeThicknessProperty.Changed,
                StrokeDashOffsetProperty.Changed, StrokeLineCapProperty.Changed,
                StrokeJoinProperty.Changed)
                .AddClassHandler<MapBaseElement>((element, _) => element.InvalidatePen());

            StrokeDashArrayProperty.Changed.AddClassHandler<MapBaseElement>((element, _) =>
            {
                element.ObserveDashes(element.MapParent is null ? null : element.StrokeDashArray);
                element.InvalidatePen();
            });

            Observable.Merge<AvaloniaPropertyChangedEventArgs>(
                FillProperty.Changed, ForegroundProperty.Changed, FontFamilyProperty.Changed,
                FontSizeProperty.Changed, FontStyleProperty.Changed, FontWeightProperty.Changed,
                StretchProperty.Changed)
                .AddClassHandler<MapBaseElement>((element, _) => element.InvalidateVisual());
        }

        public MapBaseElement()
        {
            AttachedToVisualTree += (_, _) => SetMapParent(this.FindAncestorOfType<Map>());
            DetachedFromVisualTree += (_, _) => SetMapParent(null);
            InvalidatePen();
        }

        internal void SetMapParent(Map? map)
        {
            if (ReferenceEquals(MapParent, map)) return;
            if (MapParent is { } oldMap)
                oldMap.Transform.ViewBoundsChanged -= MapViewBoundsChangedHandler;
            ObserveDashes(null);
            MapParent = map!;
            if (map is { })
            {
                ObserveDashes(StrokeDashArray);
                InvalidatePen();
                map.Transform.ViewBoundsChanged += MapViewBoundsChangedHandler;
                if (!map.Bounds.IsEmpty) OnViewChanged(map.ZoomLevel);
            }
            InvalidateVisual();
        }

        private void ObserveDashes(AvaloniaList<double>? dashes)
        {
            if (ReferenceEquals(_observedDashes, dashes)) return;
            if (_observedDashes is { } previous) previous.CollectionChanged -= DashesChanged;
            _observedDashes = dashes;
            if (dashes is { }) dashes.CollectionChanged += DashesChanged;
        }

        private void DashesChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidatePen();

        private void MapViewBoundsChangedHandler(object? sender, MapEventArgs e)
        {
            if (MapParent is { } map && ReferenceEquals(sender, map.Transform))
                OnViewChanged(e.ZoomLevel);
        }

        private void InvalidatePen()
        {
            var dashes = StrokeDashArray;
            DashStyle? dashStyle = dashes is { Count: > 0 }
                ? new DashStyle(dashes.ToArray(), StrokeDashOffset) : null;
            Pen = new Pen(Stroke, StrokeThickness, dashStyle, StrokeLineCap, StrokeJoin);
            InvalidateVisual();
        }

        protected abstract void OnViewChanged(int zoomLevel);
    }
}
