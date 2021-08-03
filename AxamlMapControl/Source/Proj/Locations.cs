using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Collections;
using AxamlMapControl.Source.Converter;

namespace AxamlMapControl.Source.Proj
{
    [TypeConverter(typeof(LocationsConverter))]
    public sealed class Locations : AvaloniaList<Location>
    {
        public Locations() { }

        public Locations(IEnumerable<Location> locations) : base(locations) { }

        public Locations(params Location[] locations) : base(locations) { }
    }
}