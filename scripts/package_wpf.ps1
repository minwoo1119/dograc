param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$SolutionPath = Join-Path $RepoRoot "apps\desktop\Dograc.sln"
$AppProjectPath = Join-Path $RepoRoot "apps\desktop\src\Dograc.App\Dograc.App.csproj"
$ArtifactsRoot = Join-Path $RepoRoot "artifacts\wpf"
$PublishDir = Join-Path $ArtifactsRoot "publish-$Runtime"
$ZipPath = Join-Path $ArtifactsRoot "dograc-wpf-$Runtime.zip"

Write-Host "=== dograc WPF Desktop Packaging ($Configuration / $Runtime) ===" -ForegroundColor Cyan

if (-not $SkipTests) {
    Write-Host "[1/3] Running solution build and unit tests..."
    dotnet build $SolutionPath --configuration $Configuration
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet test $SolutionPath --configuration $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "[2/3] Publishing self-contained WPF binary..."
if (Test-Path $PublishDir) {
    Remove-Item -Recurse -Force $PublishDir
}
New-Item -ItemType Directory -Force -Path $ArtifactsRoot | Out-Null

dotnet publish $AppProjectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$ExePath = Join-Path $PublishDir "dograc.exe"
if (-not (Test-Path $ExePath)) {
    throw "Expected executable not found: $ExePath"
}

Write-Host "[3/3] Creating portable ZIP archive..."
if (Test-Path $ZipPath) {
    Remove-Item -Force $ZipPath
}
Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $ZipPath -Force

$ZipSizeMb = [math]::Round((Get-Item $ZipPath).Length / 1MB, 2)
Write-Host "Packaging completed successfully!" -ForegroundColor Green
Write-Host "  Executable : $ExePath"
Write-Host "  Archive    : $ZipPath ($ZipSizeMb MB)"
