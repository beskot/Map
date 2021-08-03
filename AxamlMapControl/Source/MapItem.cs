using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Mixins;

namespace AxamlMapControl.Source
{
    public class MapItem : ListBoxItem
    {
        public static readonly StyledProperty<Location> LocationProperty
            = AvaloniaProperty.Register<MapItem, Location>(nameof(Location));

        public Location Location
        {
            get => GetValue(LocationProperty);
            set => SetValue(LocationProperty, value);
        }

        static MapItem()
        {
            SelectableMixin.Attach<MapItem>(IsSelectedProperty);
            PressedMixin.Attach<MapItem>();
            FocusableProperty.OverrideDefaultValue<MapItem>(true);
        }
    }
}