# Avalonia 12 migration status

Status: in progress, not build-verified. Work remains on `fix/axamlmapcontrol-stability`. The pre-migration commit is `85bc20aec91ebe20bc8c314feb2a9eaed28737e5`.

## Prepared changes

- Avalonia, Desktop and Fluent theme: 12.1.4.
- ReactiveUI.Avalonia.Reactive: 12.1.6; System.Reactive is explicit.
- RequestedThemeVariant replaces the old FluentTheme Mode setting.
- Reflection bindings remain enabled during migration.
- Removed Avalonia.Diagnostics and AttachDevTools references.
- MapItemsControl uses container override methods and a Panel template instead of the old generator and IPanel.
- MapItem relies on ListBoxItem selection/pressed behavior rather than old mixins. Its Location property forwards geographic coordinates to the placement layer.
- ItemsPresenter no longer binds removed Items, ItemTemplate or VirtualizationMode properties.
- Map item styles no longer depend on obsolete theme brush names or a TemplateBinding outside a template. Default highlight colors changed to local literal colors; application templates can override them.
- ViewLocator uses Control and nullable IDataTemplate methods.
- Placement helpers use Control, with generic attached-property registration.
- Sample data uses ItemsSource, and marker sizing lives inside the template.

## Still outstanding

- FormattedText and DrawingContext text rendering.
- Removed StringTokenizer usages and coordinate converters.
- Remaining removed or changed APIs in the source tree and remaining themes.
- Headless/UI test platform initialization.
- README synchronization.

The sample keeps its existing tile endpoint and geometry coordinates; provider availability was not verified. These changes are not proof of a successful Avalonia 12 build. Restore, build both Release and Debug, run tests, and perform a desktop smoke test before merging. Earlier CI success does not validate this migration.
