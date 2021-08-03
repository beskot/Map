using System;
using System.ComponentModel;
using System.Globalization;
using AxamlMapControl.Source.Proj;

namespace AxamlMapControl.Source.Converter
{
    public class LocationsConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            var result = new Locations();
            var values = ((string)value).Split(';', ' ');

            foreach (var s in values)
            {
                result.Add(Location.Parse(s));
            }

            return result;
        }
    }
}