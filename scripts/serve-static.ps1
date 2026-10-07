param(
    [Parameter(Mandatory = $true)]
    [string]$Root,
    [int]$Port = 4173
)

$ErrorActionPreference = "Stop"
$rootPath = [IO.Path]::GetFullPath((Join-Path (Get-Location) $Root))
if (!(Test-Path -LiteralPath $rootPath -PathType Container)) {
    throw "Static root does not exist: $rootPath"
}

$contentTypes = @{
    ".html" = "text/html; charset=utf-8"
    ".css" = "text/css; charset=utf-8"
    ".js" = "text/javascript; charset=utf-8"
    ".json" = "application/json; charset=utf-8"
    ".svg" = "image/svg+xml"
    ".png" = "image/png"
    ".jpg" = "image/jpeg"
    ".jpeg" = "image/jpeg"
    ".webp" = "image/webp"
    ".ico" = "image/x-icon"
}

$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $Port)
$listener.Start()
Write-Host "Serving $rootPath at http://127.0.0.1:$Port/"

try {
    while ($true) {
        $client = $listener.AcceptTcpClient()
        try {
            $stream = $client.GetStream()
            $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::ASCII, $false, 1024, $true)
            $requestLine = $reader.ReadLine()
            while (($header = $reader.ReadLine()) -ne "" -and $null -ne $header) { }

            $parts = $requestLine -split " "
            if ($parts.Length -lt 2 -or $parts[0] -notin @("GET", "HEAD")) {
                $status = "405 Method Not Allowed"
                $body = [Text.Encoding]::UTF8.GetBytes("Method not allowed")
                $contentType = "text/plain; charset=utf-8"
            }
            else {
                $requestPath = [Uri]::UnescapeDataString(($parts[1] -split "\?", 2)[0]).TrimStart("/")
                if ([string]::IsNullOrEmpty($requestPath) -or $requestPath.EndsWith("/")) {
                    $requestPath += "index.html"
                }
                $filePath = [IO.Path]::GetFullPath((Join-Path $rootPath $requestPath.Replace("/", [IO.Path]::DirectorySeparatorChar)))
                if (!$filePath.StartsWith($rootPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $filePath -PathType Leaf)) {
                    $status = "404 Not Found"
                    $body = [Text.Encoding]::UTF8.GetBytes("Not found")
                    $contentType = "text/plain; charset=utf-8"
                }
                else {
                    $status = "200 OK"
                    $body = [IO.File]::ReadAllBytes($filePath)
                    $extension = [IO.Path]::GetExtension($filePath).ToLowerInvariant()
                    $contentType = if ($contentTypes.ContainsKey($extension)) { $contentTypes[$extension] } else { "application/octet-stream" }
                }
            }

            $headers = "HTTP/1.1 $status`r`nContent-Type: $contentType`r`nContent-Length: $($body.Length)`r`nConnection: close`r`n`r`n"
            $headerBytes = [Text.Encoding]::ASCII.GetBytes($headers)
            $stream.Write($headerBytes, 0, $headerBytes.Length)
            if ($parts[0] -ne "HEAD") { $stream.Write($body, 0, $body.Length) }
            $stream.Flush()
        }
        catch [IO.IOException] {
            # Browsers may cancel speculative or superseded requests.
        }
        finally {
            $client.Dispose()
        }
    }
}
finally {
    $listener.Stop()
}
