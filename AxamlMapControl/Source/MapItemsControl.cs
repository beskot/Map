using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace AxamlMapControl.Source
{
    public class MapItemsControl : ListBox
    {
        private static readonly FuncTemplate<Panel> DefaultPanel =
            new(() => new MapLocationLayer());

        static MapItemsControl()
        {
            ItemsPanelProperty.OverrideDefaultValue<MapItemsControl>(DefaultPanel);
        }

        protected override Control CreateContainerForItemOverride(
            object? item, int index, object? recycleKey) => new MapItem();

        protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        {
            if (item is MapItem)
            {
                recycleKey = null;
                return false;
            }
            recycleKey = typeof(MapItem);
            return true;
        }
    }
}
