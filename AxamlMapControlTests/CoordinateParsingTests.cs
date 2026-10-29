using System;
using System.Globalization;
using AxamlMapControl.Source.Converter;
using AxamlMapControl.Source.Proj;
using Xunit;

namespace AxamlMapControlTests
{
    public class CoordinateParsingTests
    {
        [Theory]
        [InlineData("52.37, 4.9", 52.37, 4.9)]
        [InlineData("  -33.86 , 151.21  ", -33.86, 151.21)]
        [InlineData("1e1, -2E1", 10, -20)]
        [InlineData("15 16", 15, 16)]
        public void ParsesInvariantCoordinates(string text, double latitude, double longitude)
        {
            var location = Location.Parse(text);
            Assert.Equal(latitude, location.Latitude);
            Assert.Equal(longitude, location.Longitude);
        }

        [Theory]
        [InlineData("")]
        [InlineData("1")]
        [InlineData("1,2,3")]
        [InlineData("1,,2")]
        [InlineData("NaN,2")]
        [InlineData("1e999,2")]
        [InlineData("1,2 trailing")]
        public void RejectsMalformedSingleLocations(string text) =>
            Assert.Throws<FormatException>(() => Location.Parse(text));

        [Theory]
        [InlineData("1,2 3,4")]
        [InlineData("1, 2; 3, 4")]
        [InlineData(" 1,2\t3,4\n")]
        public void ParsesListsWithWhitespaceAndSemicolons(string text)
        {
            var values = Assert.IsType<Locations>(new LocationsConverter().ConvertFrom(
                null, CultureInfo.GetCultureInfo("nl-NL"), text));
            Assert.Equal(2, values.Count);
            Assert.Equal(1d, values[0].Latitude);
            Assert.Equal(2d, values[0].Longitude);
            Assert.Equal(3d, values[1].Latitude);
            Assert.Equal(4d, values[1].Longitude);
        }

        [Theory]
        [InlineData("1,2;")]
        [InlineData("1,2;;3,4")]
        [InlineData("1,2,3,4")]
        [InlineData("1,2 garbage")]
        public void RejectsMalformedLists(string text) =>
            Assert.Throws<FormatException>(() => new LocationsConverter().ConvertFromInvariantString(text));

        [Fact]
        public void EmptyListIsAllowedAndNullLocationIsRejected()
        {
            Assert.Empty(Assert.IsType<Locations>(new LocationsConverter().ConvertFromInvariantString("  ")));
            Assert.Throws<ArgumentNullException>(() => Location.Parse(null!));
        }

        [Fact]
        public void LocationRoundTripsItsFormattedString()
        {
            var original = new Location(52.37, -4.9);
            var parsed = Location.Parse(original.ToString());
            Assert.Equal(original.Latitude, parsed.Latitude);
            Assert.Equal(original.Longitude, parsed.Longitude);
        }
    }
}
