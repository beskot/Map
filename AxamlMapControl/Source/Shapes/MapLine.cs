using System;
using Avalonia;
using Avalonia.Data;
using Avalonia.Media;
using AxamlMapControl.Source.Proj;

namespace AxamlMapControl.Source.Shapes
{
    public class MapLine : MapPath
    {
        public static readonly StyledProperty<Location?> StartPointProperty =
            AvaloniaProperty.Register<MapLine, Location?>(nameof(StartPoint),
                defaultBindingMode: BindingMode.TwoWay);
        public static readonly StyledProperty<Location?> EndPointProperty =
            AvaloniaProperty.Register<MapLine, Location?>(nameof(EndPoint),
                defaultBindingMode: BindingMode.TwoWay);

        public Location? StartPoint
        {
            get => GetValue(StartPointProperty);
            set => SetValue(StartPointProperty, value);
        }
        public Location? EndPoint
        {
            get => GetValue(EndPointProperty);
            set => SetValue(EndPointProperty, value);
        }

        static MapLine()
        {
            StartPointProperty.Changed.AddClassHandler<MapLine>((line, _) => line.RefreshGeometry());
            EndPointProperty.Changed.AddClassHandler<MapLine>((line, _) => line.RefreshGeometry());
        }

        public MapLine()
        {
            Data = new PathGeometry();
        }

        protected override void Update()
        {
            var locations = StartPoint is { } start && EndPoint is { } end
                ? new[] { start, end } : Array.Empty<Location>();
            UpdateGeographicGeometry(locations, 2, false, false);
        }
    }
}
