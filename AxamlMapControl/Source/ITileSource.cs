using System;

namespace AxamlMapControl.Source
{
    public interface ITileSource
    {
        string UriStringFormat { get; }
        Uri GetUri(Tile tile);
    }
}