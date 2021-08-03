using System;
using System.Collections.ObjectModel;
using AxamlMapControl.Source.Proj;

namespace AxamlMapControlSample.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public ObservableCollection<ObjectLocation> Objects { get; set; } = new();
        
        public MainWindowViewModel()
        {            
            CreateObjects();
        }

        private void CreateObjects()
        {
            var rand = new Random();
            for (var i = 0; i < 500; i++)
            {
                var lat = rand.NextDouble() * rand.Next(-5, -2);
                var lon = rand.NextDouble() * rand.Next(-180,180);
                Objects.Add(new(new Location(lat, lon)));
            }
        }
    }

    public class ObjectLocation
    {
        public Location Position { get; set; }

        public ObjectLocation(Location pos)
        {
            Position = pos;
        }
    }
}
