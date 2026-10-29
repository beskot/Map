using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AxamlMapControlSample.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
