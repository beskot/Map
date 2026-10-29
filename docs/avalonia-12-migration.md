# Avalonia 12 migration status

Status: in progress, not build-verified. Branch: `fix/axamlmapcontrol-stability`. Pre-migration commit: `85bc20aec91ebe20bc8c314feb2a9eaed28737e5`.

## Prepared changes

- Avalonia, Desktop and Fluent theme 12.1.4; ReactiveUI.Avalonia.Reactive 12.1.6.
- Explicit System.Reactive dependency and reflection-binding compatibility mode.
- Migrated startup, map item containers, presenter templates, ViewLocator, attached-property registration and sample bindings.
- Graticule text uses the modern FormattedText constructor and DrawText(text, origin). Label layout needs visual verification.
- Independent invariant-culture coordinate parser and parser tests replace the old StringTokenizer dependency. Location equality/hashing remain a separate outstanding fix.
- UI tests use Avalonia.Headless.XUnit 12.1.4 and AvaloniaFact, with Fluent and the library styles loaded in a test application. Skia supplies actual geometry/rendering services without visible windows.
- Tests pin xunit.v3 to 3.2.2 rather than its incompatible 4.x line. The project is executable as required by xUnit v3; VSTest integration remains the intended CI path. Discovery and execution are not yet verified.
- Test parallelization is disabled to avoid interference while validating shared UI infrastructure. Pure computation and storage tests remain ordinary Fact/Theory tests.

## Still outstanding

- A final source/API and XAML compatibility pass.
- Run restore, Release and Debug builds, test discovery and the complete test suite.
- Add end-to-end template, rendering and input tests; existing tests do not establish visual correctness.
- README synchronization and a desktop smoke test.

Neither a configured test platform nor tests being present establish a successful build or test run. Check CI for the exact commit. Earlier stabilization success does not validate this migration.
