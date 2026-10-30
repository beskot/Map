using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AxamlMapControl.Source
{
    /// <summary>Optional mouse-drag inertia; regular dragging remains controlled by Map.</summary>
    public sealed class MapPanInertia : AvaloniaObject
    {
        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<MapPanInertia, Map, bool>("IsEnabled", false);
        public static readonly AttachedProperty<TimeSpan> DurationProperty =
            AvaloniaProperty.RegisterAttached<MapPanInertia, Map, TimeSpan>("Duration",
                TimeSpan.FromMilliseconds(350), coerce: (_, value) =>
                    value < TimeSpan.Zero ? TimeSpan.Zero :
                    value > TimeSpan.FromSeconds(2) ? TimeSpan.FromSeconds(2) : value);
        private static readonly ConditionalWeakTable<Map, PanState> States = new();

        static MapPanInertia()
        {
            IsEnabledProperty.Changed.AddClassHandler<Map>((map, _) =>
            {
                if (GetIsEnabled(map)) States.GetValue(map, owner => new PanState(owner));
                else if (States.TryGetValue(map, out var state))
                {
                    state.Dispose();
                    States.Remove(map);
                }
            });
        }

        public static bool GetIsEnabled(Map map) => map.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(Map map, bool value) => map.SetValue(IsEnabledProperty, value);
        public static TimeSpan GetDuration(Map map) => map.GetValue(DurationProperty);
        public static void SetDuration(Map map, TimeSpan value) => map.SetValue(DurationProperty, value);

        private sealed class PanState : IDisposable
        {
            private readonly Map _map;
            private readonly DispatcherTimer _timer;
            private readonly Stopwatch _inputClock = new();
            private readonly Stopwatch _inertiaClock = new();
            private readonly Queue<(Point Position, double Time)> _samples = new();
            private IPointer? _pointer;
            private Point _lastPosition;
            private double _lastSampleTime;
            private double _lastMotionTime;
            private bool _tracking;
            private bool _expectedCaptureLoss;
            private Vector _initialVelocity;
            private Vector _applied;
            private double _durationSeconds;

            public PanState(Map map)
            {
                _map = map;
                _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                _timer.Tick += OnTick;
                map.PointerPressed += OnPressed;
                map.PointerMoved += OnMoved;
                map.PointerCaptureLost += OnCaptureLost;
                map.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel);
                map.AddHandler(InputElement.PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
                map.PropertyChanged += OnPropertyChanged;
                map.DetachedFromVisualTree += OnDetached;
            }

            private void OnPressed(object? sender, PointerPressedEventArgs e)
            {
                Stop();
                if (!e.GetCurrentPoint(_map).Properties.IsLeftButtonPressed || e.Pointer.Captured != _map) return;
                _pointer = e.Pointer;
                _tracking = true;
                _inputClock.Restart();
                _lastPosition = e.GetPosition(_map);
                _lastSampleTime = _lastMotionTime = 0;
                _samples.Enqueue((_lastPosition, 0));
            }

            private void OnMoved(object? sender, PointerEventArgs e)
            {
                if (!_tracking || !ReferenceEquals(e.Pointer, _pointer) || e.Pointer.Captured != _map) return;
                if (!e.GetCurrentPoint(_map).Properties.IsLeftButtonPressed) { Stop(); return; }
                var position = e.GetPosition(_map);
                var now = _inputClock.Elapsed.TotalSeconds;
                var dx = position.X - _lastPosition.X;
                var dy = position.Y - _lastPosition.Y;
                if (dx * dx + dy * dy <= 0) return;
                _lastPosition = position;
                _lastSampleTime = _lastMotionTime = now;
                _samples.Enqueue((position, now));
                while (_samples.Count > 2 && _samples.Peek().Time < now - 0.12) _samples.Dequeue();
                while (_samples.Count > 32) _samples.Dequeue();
            }

            private void OnReleased(object? sender, PointerReleasedEventArgs e)
            {
                if (!_tracking || !ReferenceEquals(e.Pointer, _pointer) || e.Pointer.Captured != _map ||
                    e.GetCurrentPoint(_map).Properties.IsLeftButtonPressed) return;
                var velocity = EstimateVelocity();
                _tracking = false;
                _pointer = null;
                _samples.Clear();
                // Map releases capture later in the bubbling phase of this same event.
                _expectedCaptureLoss = true;
                var duration = GetDuration(_map).TotalSeconds;
                var speed = Math.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y);
                if (!GetIsEnabled(_map) || duration <= 0 || speed < 30) return;
                if (speed > 2500) velocity = new Vector(velocity.X * 2500 / speed, velocity.Y * 2500 / speed);
                _initialVelocity = velocity;
                _applied = new Vector();
                _durationSeconds = duration;
                _inertiaClock.Restart();
                _timer.Start();
            }

            private Vector EstimateVelocity()
            {
                if (_samples.Count < 2 || _inputClock.Elapsed.TotalSeconds - _lastMotionTime > 0.08)
                    return new Vector();
                var first = _samples.Peek();
                var elapsed = _lastSampleTime - first.Time;
                if (elapsed < 0.008) return new Vector();
                return new Vector((_lastPosition.X - first.Position.X) / elapsed,
                    (_lastPosition.Y - first.Position.Y) / elapsed);
            }

            private void OnTick(object? sender, EventArgs e)
            {
                if (!GetIsEnabled(_map) || GetDuration(_map) <= TimeSpan.Zero || _durationSeconds <= 0)
                { Stop(); return; }
                var time = Math.Min(_inertiaClock.Elapsed.TotalSeconds, _durationSeconds);
                // Integrate linear deceleration using absolute time, independent of timer jitter.
                var distanceFactor = time - time * time / (2 * _durationSeconds);
                var total = new Vector(_initialVelocity.X * distanceFactor, _initialVelocity.Y * distanceFactor);
                var delta = new Vector(total.X - _applied.X, total.Y - _applied.Y);
                _applied = total;
                try { _map.Transform.Translate(delta); }
                catch (Exception ex)
                {
                    Stop();
                    Trace.TraceError("Map pan inertia failed: {0}", ex);
                    return;
                }
                if (time >= _durationSeconds) Stop();
            }

            private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e)
            {
                if (_expectedCaptureLoss) { _expectedCaptureLoss = false; return; }
                Stop();
            }

            private void OnWheel(object? sender, PointerWheelEventArgs e) => Stop();
            private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) => Stop();

            private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
            {
                if (e.Property == Map.ZoomLevelProperty || e.Property == Map.CenterProperty ||
                    e.Property == Map.BoundsProperty || e.Property == Map.TileLayerProperty ||
                    (e.Property == DurationProperty && GetDuration(_map) <= TimeSpan.Zero)) Stop();
            }

            private void Stop()
            {
                _timer.Stop();
                _inertiaClock.Reset();
                _inputClock.Reset();
                _samples.Clear();
                _tracking = false;
                _expectedCaptureLoss = false;
                _pointer = null;
                _initialVelocity = _applied = new Vector();
            }

            public void Dispose()
            {
                Stop();
                _timer.Tick -= OnTick;
                _map.PointerPressed -= OnPressed;
                _map.PointerMoved -= OnMoved;
                _map.PointerCaptureLost -= OnCaptureLost;
                _map.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);
                _map.RemoveHandler(InputElement.PointerWheelChangedEvent, OnWheel);
                _map.PropertyChanged -= OnPropertyChanged;
                _map.DetachedFromVisualTree -= OnDetached;
            }
        }
    }
}
