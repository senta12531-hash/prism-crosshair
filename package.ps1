$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Prism.csproj'
$publish = Join-Path $PSScriptRoot 'bin/portable'
$dist = Join-Path $PSScriptRoot 'dist'

dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o $publish
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Force $dist | Out-Null
$archive = Join-Path $dist 'Prism-win-x64.zip'
Compress-Archive -Path (Join-Path $publish 'Prism.exe') -DestinationPath $archive -Force
$hash = (Get-FileHash -Algorithm SHA256 $archive).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $dist 'Prism-win-x64.sha256') -Value "$hash  Prism-win-x64.zip" -Encoding ascii
