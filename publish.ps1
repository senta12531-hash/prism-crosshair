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
    & ./package.ps1
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    gh release create "v$version" dist/Prism-win-x64.zip dist/Prism-win-x64.sha256 --target $commit --title "Prism v$version" --generate-notes
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
