using Xunit;
using AxamlMapControl.Source.Proj;

namespace AxamlMapControlTests
{
    public class MapProjectTest
    {
        [Fact]
        public void ToLocationTest()
        {
            var proj = new AxamlMapControl.Source.Proj.MapProjection();
            Assert.Equal(proj.ToLocation(new Avalonia.Point()), new Location());
        }

        [Fact]
        public void ToCartesianTest()
        {
            var proj = new AxamlMapControl.Source.Proj.MapProjection();
            Assert.Equal(proj.ToCartesian(new Location()), new Avalonia.Point(0, 0), new CartesianComparer());
        }
    }
}
