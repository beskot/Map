using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using AxamlMapControlSample.ViewModels;

namespace AxamlMapControlSample
{
    public class ViewLocator : IDataTemplate
    {
        public Control? Build(object? data)
        {
            if (data is null) return null;
            var name = data.GetType().FullName?.Replace("ViewModel", "View");
            var type = name is null ? null : data.GetType().Assembly.GetType(name);
            if (type is not null && typeof(Control).IsAssignableFrom(type))
                return (Control?)Activator.CreateInstance(type);
            return new TextBlock { Text = "Not Found: " + name };
        }

        public bool Match(object? data) => data is ViewModelBase;
    }
}
