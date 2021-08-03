using Avalonia.Controls;
using Avalonia.Controls.Generators;
using Avalonia.Controls.Templates;

namespace AxamlMapControl.Source
{
    public class MapItemsControl : ListBox
    {
        private static readonly FuncTemplate<IPanel> DefaultPanel =
            new FuncTemplate<IPanel>(() => new MapLocationLayer());

        static MapItemsControl()
        {
            ItemsPanelProperty.OverrideDefaultValue<MapItemsControl>(DefaultPanel);
        }

        protected override IItemContainerGenerator CreateItemContainerGenerator()
        {
            return new ItemContainerGenerator<MapItem>(
                this,
                MapItem.ContentProperty,
                MapItem.ContentTemplateProperty);
        }
    }
}