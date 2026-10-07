param(
    [string]$BrowserPath = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    [int]$Port = 4173
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$previewRoot = Join-Path $repositoryRoot ".pages-preview"
$siteRoot = Join-Path $previewRoot "PeopleMap"
$profileRoot = Join-Path $repositoryRoot ".browser-profile"

if (!(Test-Path -LiteralPath $BrowserPath -PathType Leaf)) {
    throw "Browser not found: $BrowserPath"
}

& (Join-Path $PSScriptRoot "build-pages.ps1") -OutputPath ".pages-preview/PeopleMap"
Copy-Item (Join-Path $repositoryRoot "tests/pages-browser-smoke.js") (Join-Path $siteRoot "js/pages-browser-smoke.js")

$indexPath = Join-Path $siteRoot "index.html"
$html = [IO.File]::ReadAllText($indexPath)
$probe = '<script>window.__peopleMapTest={calls:[],errors:[]};window.fetch=(...args)=>{window.__peopleMapTest.calls.push(String(args[0]));return Promise.reject(new Error("Unexpected fetch"))};navigator.sendBeacon=(url)=>{window.__peopleMapTest.calls.push(String(url));return false};addEventListener("error",event=>window.__peopleMapTest.errors.push(event.message));addEventListener("unhandledrejection",event=>window.__peopleMapTest.errors.push(String(event.reason)));</script>'
$runner = '<pre id="browser-smoke-result">pending</pre><script type="module" src="./js/pages-browser-smoke.js"></script>'
$html = $html.Replace('<script type="module" src="./js/app.js"></script>', "$probe`r`n  <script type=`"module`" src=`"./js/app.js`"></script>")
$html = $html.Replace("</body>", "$runner`r`n</body>")
[IO.File]::WriteAllText($indexPath, $html, [Text.UTF8Encoding]::new($false))

if (Test-Path -LiteralPath $profileRoot) { Remove-Item -LiteralPath $profileRoot -Recurse -Force }
$serverOut = Join-Path $previewRoot "server.out.log"
$serverError = Join-Path $previewRoot "server.error.log"
$server = Start-Process powershell -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\scripts\serve-static.ps1", "-Root", ".pages-preview", "-Port", $Port -WorkingDirectory $repositoryRoot -RedirectStandardOutput $serverOut -RedirectStandardError $serverError -PassThru -WindowStyle Hidden

try {
    $ready = $false
    foreach ($attempt in 1..20) {
        if ($server.HasExited) { break }
        try { $ready = (Invoke-WebRequest "http://127.0.0.1:$Port/PeopleMap/" -UseBasicParsing -TimeoutSec 1).StatusCode -eq 200 } catch { }
        if ($ready) { break }
        Start-Sleep -Milliseconds 250
    }
    if (!$ready) {
        $details = if (Test-Path $serverError) { Get-Content $serverError -Raw } else { "No server error output." }
        throw "Static server did not start. $details"
    }
    foreach ($size in @("1440,1000", "390,844")) {
        $url = "http://127.0.0.1:$Port/PeopleMap/"
        $browserProfile = Join-Path $profileRoot $size.Replace(',', '-')
        $stdoutPath = Join-Path $previewRoot "edge-$($size.Replace(',', '-')).html"
        $stderrPath = Join-Path $previewRoot "edge-$($size.Replace(',', '-')).log"
        $arguments = @("--headless=new", "--disable-gpu", "--no-first-run", "--no-default-browser-check", "--user-data-dir=$browserProfile", "--window-size=$size", "--force-device-scale-factor=1", "--virtual-time-budget=3000", "--dump-dom", $url)
        $browser = Start-Process $BrowserPath -ArgumentList $arguments -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru -WindowStyle Hidden
        if (!$browser.WaitForExit(20000)) { $browser.Kill(); throw "Browser timed out for $size." }
        $browser.WaitForExit()
        $browser.Dispose()
        $outputStream = [IO.FileStream]::new($stdoutPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
        try { $outputReader = [IO.StreamReader]::new($outputStream); $dom = $outputReader.ReadToEnd() }
        finally { $outputStream.Dispose() }
        $match = [regex]::Match($dom, '<pre id="browser-smoke-result">(?<json>.*?)</pre>', [Text.RegularExpressions.RegexOptions]::Singleline)
        if (!$match.Success -or $match.Groups["json"].Value -eq "pending") { throw "Browser smoke result was not produced for $size." }
        $json = [Net.WebUtility]::HtmlDecode($match.Groups["json"].Value)
        $result = $json | ConvertFrom-Json
        if (!$result.passed) {
            $failed = ($result.checks | Where-Object { !$_.passed } | ForEach-Object name) -join ", "
            throw "Browser checks failed for $size`: $failed"
        }
        Write-Host "PASS browser smoke $size (actual $($result.viewport.width)x$($result.viewport.height)); $($result.checks.Count) checks"
    }
}
finally {
    if (!$server.HasExited) { Stop-Process -Id $server.Id -Force }
    $server.WaitForExit()
}
