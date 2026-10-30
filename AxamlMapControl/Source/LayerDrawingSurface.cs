using System;
using Avalonia.Controls;
using Avalonia.Media;

namespace AxamlMapControl.Source
{
    internal sealed class LayerDrawingSurface : Control
    {
        private readonly Action<DrawingContext> _draw;

        public LayerDrawingSurface(Action<DrawingContext> draw)
        {
            _draw = draw ?? throw new ArgumentNullException(nameof(draw));
            IsHitTestVisible = false;
            Focusable = false;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            _draw(context);
        }
    }
}
