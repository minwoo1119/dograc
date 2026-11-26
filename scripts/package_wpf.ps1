param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "",
    [switch]$PackVelopack,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$SolutionPath = Join-Path $RepoRoot "apps\desktop\Dograc.sln"
$AppProjectPath = Join-Path $RepoRoot "apps\desktop\src\Dograc.App\Dograc.App.csproj"
$IconPath = Join-Path $RepoRoot "apps\desktop\src\Dograc.App\Assets\dograc.ico"
$ArtifactsRoot = Join-Path $RepoRoot "artifacts\wpf"
$ReleasesDir = Join-Path $RepoRoot "artifacts\releases"
$PublishDir = Join-Path $ArtifactsRoot "publish-$Runtime"
$ZipPath = Join-Path $ArtifactsRoot "dograc-wpf-$Runtime.zip"

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$csproj = Get-Content $AppProjectPath
    $Version = ($csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($Version)) { $Version = "0.1.0" }
}
$CleanVersion = $Version.TrimStart("v", "V")

Write-Host "=== dograc WPF Desktop Packaging (v$CleanVersion / $Configuration / $Runtime) ===" -ForegroundColor Cyan

if (-not $SkipTests) {
    Write-Host "[1/4] Running solution build and unit tests..."
    dotnet build $SolutionPath --configuration $Configuration -p:Version=$CleanVersion
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet test $SolutionPath --configuration $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "[2/4] Publishing self-contained WPF binary..."
if (Test-Path $PublishDir) {
    Remove-Item -Recurse -Force $PublishDir
}
New-Item -ItemType Directory -Force -Path $ArtifactsRoot | Out-Null

dotnet publish $AppProjectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:Version=$CleanVersion `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$ExePath = Join-Path $PublishDir "dograc.exe"
if (-not (Test-Path $ExePath)) {
    throw "Expected executable not found: $ExePath"
}

Write-Host "[2.5/4] Bundling FastAPI backend, compose.yaml, and .env.example (zero Git-repo dependency)..."
$BundledApiDir = Join-Path $PublishDir "apps\api"
$BundledInfraDir = Join-Path $PublishDir "infra"
New-Item -ItemType Directory -Force -Path $BundledApiDir | Out-Null
New-Item -ItemType Directory -Force -Path $BundledInfraDir | Out-Null

# Copy backend Python source & dependency lockfiles (exclude .venv, caches, tests, local DBs)
$ApiSourceDir = Join-Path $RepoRoot "apps\api"
Copy-Item -Path (Join-Path $ApiSourceDir "app") -Destination (Join-Path $BundledApiDir "app") -Recurse -Force
Get-ChildItem -Path (Join-Path $BundledApiDir "app") -Recurse -Directory -Filter "__pycache__" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
foreach ($file in @("pyproject.toml", "uv.lock", "alembic.ini")) {
    $src = Join-Path $ApiSourceDir $file
    if (Test-Path $src) { Copy-Item -Path $src -Destination $BundledApiDir -Force }
}
$AlembicSrc = Join-Path $ApiSourceDir "alembic"
if (Test-Path $AlembicSrc) {
    Copy-Item -Path $AlembicSrc -Destination (Join-Path $BundledApiDir "alembic") -Recurse -Force
}

$ComposeSrc = Join-Path $RepoRoot "infra\compose.yaml"
if (Test-Path $ComposeSrc) { Copy-Item -Path $ComposeSrc -Destination $BundledInfraDir -Force }
$EnvExampleSrc = Join-Path $RepoRoot ".env.example"
if (Test-Path $EnvExampleSrc) { Copy-Item -Path $EnvExampleSrc -Destination (Join-Path $PublishDir ".env.example") -Force }

Write-Host "[3/4] Creating portable ZIP archive..."
if (Test-Path $ZipPath) {
    Remove-Item -Force $ZipPath
}
Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $ZipPath -Force

if ($PackVelopack) {
    Write-Host "[4/4] Building Velopack installer & release feed (v$CleanVersion)..."
    $vpkCmd = Get-Command vpk -ErrorAction SilentlyContinue
    if (-not $vpkCmd) {
        $fallbackVpk = Join-Path $env:USERPROFILE ".dotnet\tools\vpk.exe"
        if (Test-Path $fallbackVpk) {
            $vpkCmd = $fallbackVpk
        } else {
            throw "Velopack CLI (vpk) not found. Install with: dotnet tool install -g vpk"
        }
    } else {
        $vpkCmd = $vpkCmd.Source
    }

    if (Test-Path $ReleasesDir) {
        Remove-Item -Recurse -Force $ReleasesDir
    }
    New-Item -ItemType Directory -Force -Path $ReleasesDir | Out-Null

    & $vpkCmd pack `
        --packId "dograc" `
        --packVersion $CleanVersion `
        --packDir $PublishDir `
        --mainExe "dograc.exe" `
        --packTitle "dograc Desktop" `
        --icon $IconPath `
        --outputDir $ReleasesDir

    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host "  Velopack Releases Dir : $ReleasesDir" -ForegroundColor Green
} else {
    Write-Host "[4/4] Skipping Velopack pack (pass -PackVelopack to generate Setup.exe & releases.win.json)."
}

$ZipSizeMb = [math]::Round((Get-Item $ZipPath).Length / 1MB, 2)
Write-Host "Packaging completed successfully!" -ForegroundColor Green
Write-Host "  Version    : v$CleanVersion"
Write-Host "  Executable : $ExePath"
Write-Host "  Archive    : $ZipPath ($ZipSizeMb MB)"
