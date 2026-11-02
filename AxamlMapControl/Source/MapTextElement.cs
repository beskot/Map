using System.Reactive.Linq;
using Avalonia;
using Avalonia.Media;

namespace AxamlMapControl.Source
{
    /// <summary>
    /// Base for text overlays. Mixed overlays reuse the styled properties with AddOwner
    /// instead of inheriting an artificial combined shape/text base.
    /// </summary>
    public abstract class MapTextElement : MapBaseElement
    {
        public static readonly StyledProperty<FontFamily> FontFamilyProperty =
            AvaloniaProperty.Register<MapTextElement, FontFamily>(nameof(FontFamily), FontFamily.Default);
        public static readonly StyledProperty<double> FontSizeProperty =
            AvaloniaProperty.Register<MapTextElement, double>(nameof(FontSize), 12);
        public static readonly StyledProperty<FontStyle> FontStyleProperty =
            AvaloniaProperty.Register<MapTextElement, FontStyle>(nameof(FontStyle));
        public static readonly StyledProperty<FontWeight> FontWeightProperty =
            AvaloniaProperty.Register<MapTextElement, FontWeight>(nameof(FontWeight), FontWeight.Normal);
        public static readonly StyledProperty<IBrush> ForegroundProperty =
            AvaloniaProperty.Register<MapTextElement, IBrush>(nameof(Foreground), Brushes.Black);

        public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
        public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
        public FontStyle FontStyle { get => GetValue(FontStyleProperty); set => SetValue(FontStyleProperty, value); }
        public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
        public IBrush Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

        static MapTextElement()
        {
            Observable.Merge<AvaloniaPropertyChangedEventArgs>(
                FontFamilyProperty.Changed, FontSizeProperty.Changed, FontStyleProperty.Changed,
                FontWeightProperty.Changed, ForegroundProperty.Changed)
                .AddClassHandler<MapTextElement>((element, _) => element.InvalidateVisual());
        }
    }
}
