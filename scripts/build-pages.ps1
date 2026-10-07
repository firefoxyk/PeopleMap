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

$generatedAppPath = Join-Path $resolvedOutput "js/app.js"
$generatedAnalyticsPath = Join-Path $resolvedOutput "js/analytics.js"
$generatedInteractionsPath = Join-Path $resolvedOutput "js/interactions.js"
foreach ($requiredFile in @($generatedAppPath, $generatedAnalyticsPath, $generatedInteractionsPath)) {
    if (!(Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required Pages asset was not copied: $requiredFile"
    }
}

$analyticsVersion = (Get-FileHash -LiteralPath $generatedAnalyticsPath -Algorithm SHA256).Hash.Substring(0, 12).ToLowerInvariant()
$interactionsVersion = (Get-FileHash -LiteralPath $generatedInteractionsPath -Algorithm SHA256).Hash.Substring(0, 12).ToLowerInvariant()
$app = [IO.File]::ReadAllText($generatedAppPath)
$app = $app.Replace('from "./analytics.js"', "from `"./analytics.js?v=$analyticsVersion`"")
$app = $app.Replace('from "./interactions.js"', "from `"./interactions.js?v=$interactionsVersion`"")
if (!$app.Contains("analytics.js?v=$analyticsVersion") -or !$app.Contains("interactions.js?v=$interactionsVersion")) {
    throw "Could not add content versions to the Pages module imports."
}

$utf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($generatedAppPath, $app, $utf8)
$appVersion = (Get-FileHash -LiteralPath $generatedAppPath -Algorithm SHA256).Hash.Substring(0, 12).ToLowerInvariant()

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
$html = $html.Replace('src="./js/app.js"', "src=`"./js/app.js?v=$appVersion`"")
if (!$html.Contains("app.js?v=$appVersion")) {
    throw "Could not add a content version to the Pages application entry point."
}

[IO.File]::WriteAllText($indexPath, $html, $utf8)
[IO.File]::WriteAllText((Join-Path $resolvedOutput ".nojekyll"), "", $utf8)
Write-Host "GitHub Pages demo created at $resolvedOutput"
