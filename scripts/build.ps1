[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $repositoryRoot 'src'
$outputDirectory = Join-Path $repositoryRoot ('build\' + $Configuration)
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The 64-bit .NET Framework C# compiler was not found. Install the .NET Framework 4.8 Developer Pack.'
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$outputFile = Join-Path $outputDirectory 'WordlistViewer.exe'
$manifest = Join-Path $sourceRoot 'app.manifest'
$sourceNames = @(
    'Program.cs',
    'Localization.cs',
    'Localization.MoreLanguages.cs',
    'MainFormV2.cs',
    'HugeFileEngine.cs',
    'ViewerSession.cs',
    'NavigationIndex.cs',
    'DisplayFormatter.cs',
    'LongLineForm.cs',
    'VirtualLineViewer.cs',
    'ViewerChunk.cs',
    'FullSearchResultStore.cs'
)
$sources = $sourceNames | ForEach-Object { Join-Path $sourceRoot $_ }

$arguments = @(
    '/nologo',
    '/target:winexe',
    '/platform:x64',
    '/langversion:5',
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Drawing.dll',
    "/win32manifest:$manifest",
    "/out:$outputFile"
)
if ($Configuration -eq 'Release') {
    $arguments += '/optimize+'
} else {
    $arguments += @('/optimize-', '/debug:full')
}

& $compiler @arguments @sources
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($outputFile).FileVersion
Write-Host "Built $outputFile (version $version)"

