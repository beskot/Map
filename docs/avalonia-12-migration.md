# Avalonia 12 migration status

Status: in progress, not build-verified.

The migration is performed on `fix/axamlmapcontrol-stability`, not on a separate branch. The last commit before migration is `85bc20aec91ebe20bc8c314feb2a9eaed28737e5`.

## Dependency and startup stage

- Avalonia and Avalonia.Desktop: 12.1.4.
- Explicit Fluent theme package: 12.1.4.
- Legacy Avalonia.ReactiveUI replaced with ReactiveUI.Avalonia.Reactive 12.1.6 for the existing System.Reactive-based sample.
- System.Reactive is an explicit library dependency.
- Reflection bindings remain enabled explicitly during migration.
- The application selects its light theme through RequestedThemeVariant.
- The removed Avalonia.Diagnostics package and AttachDevTools call are no longer used. No paid developer tools were added.
- .NET 10 remains the target.

## Outstanding migration work

This stage is not expected to establish a working Avalonia 12 build on its own. Review and migrate the remaining 0.10 APIs before calling the upgrade complete:

- MapItemsControl container generation and MapItem mixins.
- Removed control interfaces and changed property-registration APIs.
- FormattedText construction and DrawingContext text drawing.
- Removed StringTokenizer usages in coordinate parsing and converters.
- Themes, item bindings, ViewLocator and sample XAML.
- UI test platform initialization and test-framework compatibility.
- Root README examples and dependency notes.

Run restore, Release and Debug builds, automated tests and a desktop smoke test. Check the CI result for the exact migration commit; success of an earlier stabilization commit is not evidence for this upgrade.
