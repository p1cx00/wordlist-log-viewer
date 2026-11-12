[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $repositoryRoot 'src'
$testRoot = Join-Path $repositoryRoot 'tests'
$outputDirectory = Join-Path $repositoryRoot 'build\Tests'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The 64-bit .NET Framework C# compiler was not found. Install the .NET Framework 4.8 Developer Pack.'
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$outputFile = Join-Path $outputDirectory 'EngineTests.exe'
$sourceNames = @(
    'Localization.cs',
    'Localization.MoreLanguages.cs',
    'HugeFileEngine.cs',
    'ViewerSession.cs',
    'NavigationIndex.cs',
    'DisplayFormatter.cs',
    'ViewerChunk.cs',
    'FullSearchResultStore.cs',
    'VirtualLineViewer.cs',
    'LongLineForm.cs'
)
$sources = @((Join-Path $testRoot 'EngineTests.cs')) +
    @($sourceNames | ForEach-Object { Join-Path $sourceRoot $_ })

& $compiler /nologo /target:exe /platform:x64 /optimize+ /langversion:5 `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
    "/out:$outputFile" @sources
if ($LASTEXITCODE -ne 0) {
    throw "Test compilation failed with exit code $LASTEXITCODE."
}

& $outputFile
if ($LASTEXITCODE -ne 0) {
    throw "Tests failed with exit code $LASTEXITCODE."
}

