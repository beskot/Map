using System;
using System.Globalization;
using System.Threading;
using Avalonia;

namespace AxamlMapControl.Source
{
    public class TileSource : AvaloniaObject, ITileSource
    {
        public static readonly StyledProperty<string> UriStringFormatProperty =
            AvaloniaProperty.Register<TileSource, string>(nameof(UriStringFormat), string.Empty);

        private string _uriStringFormat = string.Empty;

        public string UriStringFormat
        {
            get => Volatile.Read(ref _uriStringFormat);
            set => SetValue(UriStringFormatProperty, value);
        }

        static TileSource()
        {
            UriStringFormatProperty.Changed.AddClassHandler<TileSource>((source, e) =>
                Volatile.Write(ref source._uriStringFormat,
                    e.NewValue is string value ? value : string.Empty));
        }

        public Uri GetUri(Tile tile)
        {
            if (tile is null) throw new ArgumentNullException(nameof(tile));
            var format = UriStringFormat;
            if (string.IsNullOrWhiteSpace(format))
                throw new InvalidOperationException("A tile URI format must be configured.");
            return new Uri(format
                .Replace("{z}", tile.Zoom.ToString(CultureInfo.InvariantCulture))
                .Replace("{x}", tile.X.ToString(CultureInfo.InvariantCulture))
                .Replace("{y}", tile.Y.ToString(CultureInfo.InvariantCulture)), UriKind.Absolute);
        }
    }
}
