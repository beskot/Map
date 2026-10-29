using Avalonia;
using Avalonia.Controls;
using AxamlMapControl.Source.Proj;
using Location = AxamlMapControl.Source.Proj.Location;

namespace AxamlMapControl.Source
{
    public class MapItem : ListBoxItem
    {
        public static readonly StyledProperty<Location?> LocationProperty =
            AvaloniaProperty.Register<MapItem, Location?>(nameof(Location));

        public Location? Location
        {
            get => GetValue(LocationProperty);
            set => SetValue(LocationProperty, value);
        }

        static MapItem()
        {
            FocusableProperty.OverrideDefaultValue<MapItem>(true);
            LocationProperty.Changed.AddClassHandler<MapItem>((item, e) =>
                MapLocationLayer.SetLocation(item, e.NewValue is Location location ? location : null));
        }
    }
}
