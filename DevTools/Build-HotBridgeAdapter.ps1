param(
    [string]$Destination = (Join-Path $PSScriptRoot 'BridgeAdapters'),
    [string]$PublisherPath = (Join-Path $PSScriptRoot '..\..\RimWorldDevBridge\DevTools\Publish-RimWorldBridgeAdapter.ps1')
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'BridgeAdapter\HorticultureNovelSeeds.BridgeAdapter.csproj'
$build = Join-Path $PSScriptRoot 'BridgeAdapter\Build'
$destination = [IO.Path]::GetFullPath($Destination)
$publisher = [IO.Path]::GetFullPath($PublisherPath)
$source = Join-Path $PSScriptRoot 'BridgeAdapter\HorticultureBridgeAdapter.cs'
$stamp = Get-Date -Format 'yyyyMMddHHmmssfff'
$assemblyName = "HorticultureNovelSeeds.BridgeAdapter.$stamp"

New-Item -ItemType Directory -Force -Path $build | Out-Null
dotnet build $project -c Release "-p:AssemblyName=$assemblyName" "-p:OutputPath=$build"
if ($LASTEXITCODE -ne 0) { throw "Hot adapter build failed with exit code $LASTEXITCODE." }
if (-not (Test-Path -LiteralPath $publisher -PathType Leaf)) { throw "Bridge adapter publisher not found: $publisher" }

$built = Join-Path $build ($assemblyName + '.dll')
$text = [IO.File]::ReadAllText($source)
$specs = @([regex]::Matches($text, '"(?<spec>[A-Z][A-Z0-9_]*\|[RW]\|[^"\r\n]+)"') |
    ForEach-Object { $_.Groups['spec'].Value })
& $publisher -AssemblyPath $built -Destination $destination -AdapterId 'HorticultureNovelSeeds' `
    -DisplayName 'Horticulture Novel Seeds' -Version '1.8.4' -Generation $stamp `
    -ProviderType 'HorticultureNovelSeeds.HorticultureBridgeAdapter' -CommandSpecs $specs `
    -RequiredPackageIds @('lan.horticulture.novelseeds') -NoMapCommands @('HNS_ADAPTER_STATUS','HNS_CROSS_REGRESSIONS','HNS_TRAIT_CATALOG_REGRESSIONS','HNS_BREEDING_MIX_DIAGNOSTIC','HNS_EVENT_ROUTING_DIAGNOSTIC','HNS_EVENT_ROUTING_REGRESSION') `
    -UiOnlyCommands @('HNS_OPEN_GROWER_MENU','HNS_OPEN_REGISTRY','HNS_SET_REGISTRY_PAGE', `
        'HNS_SET_REGISTRY_COMPARE','HNS_SET_REGISTRY_SCOPE','HNS_OPEN_MASK_EDITOR','HNS_CAPTURE_UI','HNS_DPA_OPEN') `
    -TemporaryCommands @('HNS_IMMUTABLE_GAMEPLAY_TEST','HNS_NICE_FIXTURE_OPEN','HNS_NICE_FIXTURE_FOCUS', `
        'HNS_NICE_FIXTURE_CLEANUP','HNS_DEV_MASK_GIZMO','HNS_DEV_RANDOM_GRID','HNS_DPA_START','HNS_DPA_SAMPLE') `
    -DestructiveCommands @('HNS_GENERATE_AUTO_MASKS','HNS_MASK_EDITOR_ACTION','HNS_LOAD_TEST_SAVE', `
        'HNS_DISABLE_PATCHES','HNS_DISABLE_GROWTH_OWNER') `
    -ExpensiveCommands @('HNS_BALANCE_TEST','HNS_MASK_REGRESSIONS','HNS_CROSS_REGRESSIONS','HNS_TRAIT_CATALOG_REGRESSIONS','HNS_BREEDING_MIX_DIAGNOSTIC','HNS_RESOURCE_JOB_REGRESSION','HNS_RESOURCE_JOB_STATUS','HNS_RESOURCE_JOB_INTERRUPT','HNS_RESOURCE_JOB_RETRY','HNS_RESOURCE_JOB_CLEANUP','HNS_EVENT_ROUTING_DIAGNOSTIC','HNS_EVENT_ROUTING_REGRESSION','HNS_EVENT_ROUTING_DUPLICATE_TEST','HNS_EVENT_INVALIDATION_PERF','HNS_EXPORT_MASK_DIAGNOSTIC', `
        'HNS_GENERATE_AUTO_MASKS','HNS_CAPTURE_UI','HNS_SAVE_TEST','HNS_PLANT_PATCHES','HNS_DPA_ENTRIES','HNS_DPA_SAMPLE') `
    -SimulationCommands @('HNS_IMMUTABLE_GAMEPLAY_TEST','HNS_DEV_RANDOM_GRID','HNS_LOAD_TEST_SAVE') `
    -ChangeSummary 'Canonical plant event identity, bounded deduplication, tree knowledge, and targeted invalidation diagnostics.'
if ($LASTEXITCODE -ne 0) { throw "Hot adapter publication failed with exit code $LASTEXITCODE." }
