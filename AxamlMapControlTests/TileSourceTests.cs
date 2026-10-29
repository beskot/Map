using System;
using AxamlMapControl.Source;
using Xunit;

namespace AxamlMapControlTests
{
    public class TileSourceTests
    {
        [Fact]
        public void SourcesKeepIndependentFormatsAndValidateInput()
        {
            var first = new TileSource
            {
                UriStringFormat = "https://first.example/{z}/{x}/{y}.png"
            };
            var second = new TileSource
            {
                UriStringFormat = "https://second.example/{z}/{x}/{y}.png"
            };
            var tile = new Tile(3, 2, 1);
            Assert.Equal("https://first.example/3/2/1.png", first.GetUri(tile).AbsoluteUri);
            Assert.Equal("https://second.example/3/2/1.png", second.GetUri(tile).AbsoluteUri);
            second.UriStringFormat = "https://changed.example/{z}/{x}/{y}.png";
            Assert.Equal("https://first.example/3/2/1.png", first.GetUri(tile).AbsoluteUri);
            Assert.Equal("https://changed.example/3/2/1.png", second.GetUri(tile).AbsoluteUri);
            first.UriStringFormat = string.Empty;
            Assert.Throws<InvalidOperationException>(() => first.GetUri(tile));
            Assert.Throws<ArgumentNullException>(() => second.GetUri(null!));
        }
    }
}
