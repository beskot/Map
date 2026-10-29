using System.Collections.Specialized;
using Avalonia;
using Avalonia.Media;
using AxamlMapControl.Source.Proj;

namespace AxamlMapControl.Source.Shapes
{
    public class MapPolyline : MapPath
    {
        public static readonly StyledProperty<Locations> LocationsProperty =
            AvaloniaProperty.Register<MapPolyline, Locations>(nameof(Locations));

        public Locations Locations
        {
            get => GetValue(LocationsProperty);
            set => SetValue(LocationsProperty, value);
        }

        private Locations? _observedLocations;
        private bool _observeChanges = true;

        static MapPolyline()
        {
            LocationsProperty.Changed.AddClassHandler<MapPolyline>((line, _) =>
            {
                line.ObserveLocations(line._observeChanges ? line.Locations : null);
                line.RefreshGeometry();
            });
        }

        public MapPolyline()
        {
            Data = new PathGeometry();
            Locations = new Locations();
        }

        private void ObserveLocations(Locations? locations)
        {
            if (ReferenceEquals(_observedLocations, locations)) return;
            if (_observedLocations is { } previous) previous.CollectionChanged -= LocationsChanged;
            _observedLocations = locations;
            if (locations is { }) locations.CollectionChanged += LocationsChanged;
        }

        private void LocationsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshGeometry();

        protected override void OnMapParentChanged(Map? previous, Map? current)
        {
            base.OnMapParentChanged(previous, current);
            _observeChanges = current is not null;
            ObserveLocations(_observeChanges ? Locations : null);
        }

        protected override void Update() => Update(false, false);

        protected void Update(bool isClosed, bool isFilled) =>
            UpdateGeographicGeometry(Locations, isClosed ? 3 : 2, isClosed, isFilled);
    }
}
