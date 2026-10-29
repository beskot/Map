using System;
using Avalonia;
using AxamlMapControl.Source.Proj;
using Xunit;

namespace AxamlMapControlTests
{
    public class MapProjectionBoundaryTests
    {
        [Fact]
        public void OriginMapsExactlyToZero()
        {
            var projection = new MapProjection();
            Assert.Equal(new Point(0, 0), projection.ToCartesian(new Location()));
            var location = projection.ToLocation(new Point(0, 0));
            Assert.Equal(0d, location.Latitude);
            Assert.Equal(0d, location.Longitude);
        }

        [Theory]
        [InlineData(52.37, 4.9)]
        [InlineData(-33.86, 151.21)]
        [InlineData(80, -170)]
        [InlineData(-80, 170)]
        public void RoundTripPreservesCoordinatesInsideWorld(double latitude, double longitude)
        {
            var projection = new MapProjection();
            var result = projection.ToLocation(projection.ToCartesian(new Location(latitude, longitude)));
            Assert.InRange(Math.Abs(result.Latitude - latitude), 0d, 1e-9);
            Assert.InRange(Math.Abs(result.Longitude - longitude), 0d, 1e-9);
        }

        [Theory]
        [InlineData(90)]
        [InlineData(-90)]
        [InlineData(120)]
        [InlineData(-120)]
        public void PolarInputClampsToFiniteWorldEdge(double latitude)
        {
            var projection = new MapProjection();
            var point = projection.ToCartesian(new Location(latitude, 0));
            Assert.True(double.IsFinite(point.X));
            Assert.True(double.IsFinite(point.Y));
            Assert.InRange(Math.Abs(Math.Abs(point.Y) - MapProjection.OriginShift), 0d, 1e-6);
            Assert.Equal(Math.Sign(latitude), Math.Sign(point.Y));
            var result = projection.ToLocation(point);
            Assert.InRange(Math.Abs(Math.Abs(result.Latitude) - MapProjection.MaxLatitude), 0d, 1e-9);
            var scale = projection.GetRelativeScale(new Location(latitude, 0));
            Assert.True(double.IsFinite(scale.X));
            Assert.True(scale.X > 0);
            Assert.Equal(scale.X, scale.Y);
        }

        [Theory]
        [InlineData(540)]
        [InlineData(-540)]
        public void LongitudeRemainsUnwrapped(double longitude)
        {
            var projection = new MapProjection();
            var result = projection.ToLocation(projection.ToCartesian(new Location(0, longitude)));
            Assert.InRange(Math.Abs(result.Longitude - longitude), 0d, 1e-9);
        }

        [Fact]
        public void InverseClampsFiniteVerticalCoordinatesBeyondWorld()
        {
            var projection = new MapProjection();
            Assert.Equal(MapProjection.MaxLatitude,
                projection.ToLocation(new Point(0, double.MaxValue)).Latitude);
            Assert.Equal(MapProjection.MinLatitude,
                projection.ToLocation(new Point(0, -double.MaxValue)).Latitude);
        }

        [Theory]
        [InlineData(double.NaN, 0)]
        [InlineData(double.PositiveInfinity, 0)]
        [InlineData(double.NegativeInfinity, 0)]
        [InlineData(0, double.NaN)]
        [InlineData(0, double.PositiveInfinity)]
        public void NonFiniteInputsAreRejected(double first, double second)
        {
            var projection = new MapProjection();
            var location = new Location(first, second);
            Assert.Throws<ArgumentOutOfRangeException>(() => projection.ToCartesian(location));
            Assert.Throws<ArgumentOutOfRangeException>(() => projection.GetRelativeScale(location));
            Assert.Throws<ArgumentOutOfRangeException>(() => projection.ToLocation(new Point(first, second)));
        }

        [Fact]
        public void NullAndOverflowingLongitudeAreRejected()
        {
            var projection = new MapProjection();
            Assert.Throws<ArgumentNullException>(() => projection.ToCartesian(null!));
            Assert.Throws<ArgumentNullException>(() => projection.GetRelativeScale(null!));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                projection.ToCartesian(new Location(0, double.MaxValue)));
        }
    }
}
