param([string]$InnoCompiler)

$ErrorActionPreference = 'Stop'
if (!$InnoCompiler) {
    $InnoCompiler = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Path
}
if (!$InnoCompiler) {
    $InnoCompiler = @(
        "$env:ProgramFiles\Inno Setup 7\ISCC.exe"
        "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe"
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
if (!$InnoCompiler -or !(Test-Path -LiteralPath $InnoCompiler -PathType Leaf)) {
    throw 'Install Inno Setup 6.3+ or pass -InnoCompiler with the path to ISCC.exe.'
}

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
$version = ([xml](Get-Content $project -Raw)).Project.PropertyGroup.Version
$setupScript = Join-Path $PSScriptRoot 'Prism.iss'
& $InnoCompiler "--define=PrismVersion=$version" $setupScript
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

foreach ($filename in @('Prism-win-x64.zip', 'Prism-Setup-win-x64.exe')) {
    $artifact = Join-Path $dist $filename
    $checksum = [IO.Path]::ChangeExtension($artifact, 'sha256')
    $hash = (Get-FileHash -Algorithm SHA256 $artifact).Hash.ToLowerInvariant()
    Set-Content -LiteralPath $checksum -Value "$hash  $filename" -Encoding ascii
}
