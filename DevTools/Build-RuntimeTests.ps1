param(
    [switch]$SkipGameplayBuild,
    [switch]$SkipDeploy
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$gameProject = Join-Path $root 'Source\HorticultureNovelSeeds.csproj'
$testProject = Join-Path $PSScriptRoot 'RuntimeTests\HorticultureNovelSeeds.RuntimeTests.csproj'
$testAssembly = Join-Path $PSScriptRoot 'RuntimeTests\Build\HorticultureNovelSeeds.RuntimeTests.v12.dll'
$assemblyDirectory = Join-Path $root '1.6\Assemblies'
$deployedAssembly = Join-Path $assemblyDirectory 'HorticultureNovelSeeds.RuntimeTests.v12.dll'

if (-not $SkipGameplayBuild) {
    dotnet build $gameProject --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Horticulture Release build failed.' }
}

dotnet build $testProject --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Horticulture runtime test assembly build failed.' }
if (-not (Test-Path -LiteralPath $testAssembly -PathType Leaf)) { throw "Runtime test assembly was not produced: $testAssembly" }
if (-not $SkipDeploy) {
    $sourceHash = (Get-FileHash -LiteralPath $testAssembly -Algorithm SHA256).Hash
    if (Test-Path -LiteralPath $deployedAssembly -PathType Leaf) {
        $deployedHash = (Get-FileHash -LiteralPath $deployedAssembly -Algorithm SHA256).Hash
        if ($sourceHash -eq $deployedHash) {
            Write-Output "Runtime test assembly already loaded at matching revision; skipped deploy: $deployedAssembly"
        }
        else {
            try {
                Copy-Item -LiteralPath $testAssembly -Destination $deployedAssembly -Force
                Write-Output "Runtime test assembly deployed: $deployedAssembly"
            }
            catch {
                throw "Runtime test assembly is locked and differs from the built revision: $deployedAssembly"
            }
        }
    }
    else {
        Copy-Item -LiteralPath $testAssembly -Destination $deployedAssembly -Force
        Write-Output "Runtime test assembly deployed: $deployedAssembly"
    }
}
else {
    Write-Output "Runtime test assembly already loaded; skipped deploy: $deployedAssembly"
}
