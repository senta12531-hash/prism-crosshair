param([string]$InnoCompiler, [string]$NotesFile)

$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $workingTree = git status --porcelain
    if ($LASTEXITCODE -ne 0 -or $workingTree) {
        throw 'Commit and push the source before publishing a release.'
    }

    $commit = git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $remote = gh repo view --json nameWithOwner --jq .nameWithOwner
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    gh api "repos/$remote/commits/$commit" --silent
    if ($LASTEXITCODE -ne 0) {
        throw 'The current commit must exist on GitHub before publishing a release.'
    }

    $version = ([xml](Get-Content Prism.csproj -Raw)).Project.PropertyGroup.Version
    & ./package.ps1 -InnoCompiler $InnoCompiler
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $assets = @('dist/Prism-Setup-win-x64.exe', 'dist/Prism-Setup-win-x64.sha256',
                'dist/Prism-win-x64.zip', 'dist/Prism-win-x64.sha256')
    $notes = @('--generate-notes')
    if ($NotesFile) { $notes = @('--notes-file', $NotesFile) }
    gh release create "v$version" @assets --target $commit --title "Prism v$version" @notes
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
