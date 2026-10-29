using System;
using System.ComponentModel;
using System.Globalization;
using AxamlMapControl.Source.Proj;

namespace AxamlMapControl.Source.Converter
{
    public class LocationsConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
            sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
            value is string text ? CoordinateTextParser.ParseLocations(text)
                : base.ConvertFrom(context, culture, value);
    }
}
