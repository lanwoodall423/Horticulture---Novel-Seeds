# Horticulture testing

Horticulture owns and executes all Horticulture-specific tests. DevBridge2 coordinates RimWorld process generations, readiness, and shared test leases; it does not implement or execute Horticulture scenarios.

## Structural and unit validation

From the mod root:

```powershell
dotnet build .\Source\HorticultureNovelSeeds.csproj --configuration Release
dotnet restore .\DevTools\RuntimeTests\HorticultureNovelSeeds.RuntimeTests.csproj
dotnet build .\DevTools\RuntimeTests\HorticultureNovelSeeds.RuntimeTests.csproj --configuration Release --no-restore
.\DevTools\Test-ReleasePackage.ps1
```

Run focused source/regression checks as needed:

```powershell
Get-ChildItem .\DevTools\Verify-*.ps1 | ForEach-Object { & $_.FullName }
```

The four focused regression source files remain development-only and are excluded from the production project. They are not exposed through DevBridge2.

## Runtime validation

Use the repository-owned harness:

```powershell
.\DevTools\Run-RuntimeTests.ps1 -Scenario complete
```

The harness builds the production and test assemblies, places the test assembly in the local RimWorld assembly directory for the test generation, requests a DevBridge2 restart, waits for a playable quicktest map, writes one Horticulture request, acquires a lease, waits for the Horticulture result, prints it, releases the exact lease, and removes the request/test assembly. It never launches, kills, or restarts RimWorld directly.

For a focused scenario, use one of `startup`, `clean-default`, `ordinary-crop`, `sowable-tree`, `cross-pollination`, `produce-processing`, `knowledge`, `save-reload`, `negative`, `long-running`, `ux-discovery`, `registry-scale`, or `rc-performance`:

```powershell
.\DevTools\Run-RuntimeTests.ps1 -Scenario ordinary-crop
```

Automatic mask coverage is owned by Horticulture and runs in-game through the same harness:

```powershell
.\DevTools\Run-RuntimeTests.ps1 -Scenario auto-mask-suite
```

To publish a real Unity/RimWorld-generated bundle, use the Horticulture publisher. It builds the
Release DLL, asks DevBridge2 only to coordinate the launch/readiness lease, runs the Horticulture
export scenario, validates every identity, and stages the XML plus manifest. Add `-InstallBundle`
only after reviewing the staging report:

```powershell
.\DevTools\Publish-AutoMaskBundle.ps1 -InstallBundle
```

If a coordinator command is interrupted, run `DevBridge.cmd wait-ready` before obtaining a new lease. A new production DLL, Def, Harmony, serialized-type, or core change requires `DevBridge.cmd restart` before testing. DevBridge2 coordinates process state only; all mask generation, assertions, export, and validation belong to Horticulture.

## Cleanup

The harness removes its request, result-independent checkpoint, and test DLL in `finally`. If a shell is interrupted, remove only these explicit generated paths from `DevBridge2\Runtime` and `1.6\Assemblies`, then run `Test-ReleasePackage.ps1`. Never remove the whole runtime or assembly directory.

See [RUNTIME_TESTS.md](RUNTIME_TESTS.md) for scenarios and the result schema.
