# Avalonia 12 migration status

Status: in progress, not build-verified. Branch: `fix/axamlmapcontrol-stability`. Pre-migration commit: `85bc20aec91ebe20bc8c314feb2a9eaed28737e5`.

## Prepared changes

- Avalonia, Desktop and Fluent theme 12.1.4; ReactiveUI.Avalonia.Reactive 12.1.6.
- System.Reactive is an explicit dependency; reflection bindings remain enabled.
- Updated startup and RequestedThemeVariant; removed legacy diagnostics calls.
- Migrated map item container creation, presenter templates, ViewLocator, attached-property registration and sample ItemsSource binding.
- Graticule labels now use the culture/flow-direction/typeface/brush FormattedText constructor and DrawText(text, origin). The old fixed text box constraint is no longer applied; label layout needs visual verification.
- Location.Parse no longer depends on Avalonia's old StringTokenizer.
- Coordinate lists accept comma-separated pairs, separated by whitespace or semicolons. Spaces around commas are supported. Decimal points are invariant-culture dots; scientific notation is allowed. Empty lists are allowed; malformed and non-finite parsed values are rejected.
- Added coordinate parser tests. Equality, hashing and mutability of Location are intentionally unchanged in this migration stage and still need their separate review.

## Still outstanding

- Remaining removed or changed APIs and remaining themes.
- Headless/UI test initialization.
- README synchronization.
- Restore, Release and Debug builds, all tests, and a desktop smoke test.

Neither these edits nor tests being present establish a successful Avalonia 12 build. Check CI for the exact commit. Earlier stabilization CI success does not validate the migration.
