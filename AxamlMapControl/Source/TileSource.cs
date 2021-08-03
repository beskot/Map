using System;
using Avalonia;

namespace AxamlMapControl.Source
{
    /// <summary>
    /// Tile source.
    /// </summary>
    public class TileSource : AvaloniaObject, ITileSource
    {
        public static readonly AvaloniaProperty<string> UriStringFormatProperty
            = AvaloniaProperty.Register<TileSource, string>(nameof(UriStringFormat), string.Empty, false, Avalonia.Data.BindingMode.OneTime);

        public string UriStringFormat
        {
            get => _uriStringFormat ?? string.Empty;
            set => SetValue(UriStringFormatProperty, value);
        }

        private string? _uriStringFormat;

        private System.Threading.SynchronizationContext _context = new();

        public TileSource()
        {
            UriStringFormatProperty.Changed.AddClassHandler<TileSource>((_, e) => _uriStringFormat = (e.NewValue is string s) ? s : string.Empty);
        }

        private Uri GetUri(int z, int x, int y)
        {
            if (string.IsNullOrEmpty(_uriStringFormat))
            {
                throw new NullReferenceException("UriStringFormat is null or empty.");
            }

            var tileUri = _uriStringFormat
                .Replace("{z}", z.ToString())
                .Replace("{x}", x.ToString())
                .Replace("{y}", y.ToString());

            return new Uri(tileUri);
        }

        public Uri GetUri(Tile tile)
        {
            return GetUri(tile.Zoom, tile.X, tile.Y);
        }

        private char GetServer()
        {
            return new[] { 'a', 'b', 'c' }[new Random().Next(0, 2)];
        }
    }
}