using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AxamlMapControl.Source.Proj
{
    internal static class CoordinateTextParser
    {
        private const string Number = @"[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?";
        private static readonly Regex Single = new(
            @"\A\s*(?<lat>" + Number + @")(?:\s*,\s*|\s+)(?<lon>" + Number + @")\s*\z",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        private static readonly Regex ListPair = new(
            @"\G\s*(?<lat>" + Number + @")\s*,\s*(?<lon>" + Number + @")",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        public static Location ParseLocation(string text)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));
            var match = Single.Match(text);
            if (!match.Success) throw new FormatException("Expected latitude, longitude.");
            return FromMatch(match);
        }

        private static Location FromMatch(Match match)
        {
            if (!double.TryParse(match.Groups["lat"].Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var latitude) ||
                !double.TryParse(match.Groups["lon"].Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var longitude) ||
                !double.IsFinite(latitude) || !double.IsFinite(longitude))
                throw new FormatException("Coordinates must be finite invariant-culture numbers.");
            return new Location(latitude, longitude);
        }

        public static Locations ParseLocations(string text)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));
            text = text.Trim();
            var result = new Locations();
            var position = 0;
            while (position < text.Length)
            {
                var match = ListPair.Match(text, position);
                if (!match.Success || match.Index != position)
                    throw new FormatException("Expected comma-separated coordinate pairs.");
                result.Add(FromMatch(match));
                position += match.Length;
                var separatorStart = position;
                while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
                if (position == text.Length) break;
                if (text[position] == ';')
                {
                    position++;
                    while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
                    if (position == text.Length) throw new FormatException("Missing coordinate after separator.");
                }
                else if (position == separatorStart)
                {
                    throw new FormatException("Coordinate pairs must be separated by whitespace or a semicolon.");
                }
            }
            return result;
        }
    }
}
