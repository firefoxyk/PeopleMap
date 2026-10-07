param(
    [string]$OutputPath = ".pages-dist"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$sourcePath = Join-Path $repositoryRoot "src/PeopleMap.Api/wwwroot"
$resolvedOutput = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputPath))

if (!$resolvedOutput.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    $resolvedOutput -eq $repositoryRoot -or $resolvedOutput -eq $sourcePath) {
    throw "OutputPath must be a generated directory inside the repository."
}

if (Test-Path -LiteralPath $resolvedOutput) {
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}

New-Item -ItemType Directory -Path $resolvedOutput | Out-Null
Copy-Item -Path (Join-Path $sourcePath "*") -Destination $resolvedOutput -Recurse

$indexPath = Join-Path $resolvedOutput "index.html"
$html = [IO.File]::ReadAllText($indexPath)
$marker = '  <meta name="peoplemap-mode" content="demo">'
if ($html.Contains('name="peoplemap-mode"')) {
    throw "The source index already contains a demo mode marker."
}
$html = $html.Replace('  <meta name="theme-color" content="#f7f6f2">', "  <meta name=`"theme-color`" content=`"#f7f6f2`">`r`n$marker")
if (!$html.Contains($marker)) {
    throw "Could not inject the demo mode marker."
}

$utf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($indexPath, $html, $utf8)
[IO.File]::WriteAllText((Join-Path $resolvedOutput ".nojekyll"), "", $utf8)
Write-Host "GitHub Pages demo created at $resolvedOutput"
