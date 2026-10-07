using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;

var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var apiAssembly = Path.Combine(repositoryRoot, "src", "PeopleMap.Api", "bin", "Release", "net8.0", "PeopleMap.Api.dll");
const string baseUrl = "http://127.0.0.1:5199";

using var process = Process.Start(new ProcessStartInfo
{
    FileName = "dotnet",
    Arguments = $"\"{apiAssembly}\" --urls {baseUrl}",
    WorkingDirectory = repositoryRoot,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
    CreateNoWindow = true
}) ?? throw new InvalidOperationException("Could not start PeopleMap.Api.");

try
{
    using var client = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(5) };
    var ready = false;
    for (var attempt = 0; attempt < 30; attempt++)
    {
        try
        {
            var health = await client.GetAsync("/api/health");
            if (health.IsSuccessStatusCode) { ready = true; break; }
        }
        catch (HttpRequestException) { }
        await Task.Delay(250);
    }
    Assert(ready, "API starts and health endpoint responds.");

    var page = await client.GetStringAsync("/");
    Assert(page.Contains("Remember everyone.", StringComparison.Ordinal), "Landing page is served.");
    Assert(page.Contains("data-section=\"relationships\"", StringComparison.Ordinal), "Relationship section is present.");
    Assert(page.Contains("data-section=\"hierarchical_tags\"", StringComparison.Ordinal), "Hierarchical tags section is present.");
    Assert(page.Contains("data-section=\"relationship_status\"", StringComparison.Ordinal), "Relationship status section is present.");
    Assert(page.Contains("data-section=\"saved_views\"", StringComparison.Ordinal), "Saved views section is present.");
    Assert(page.Contains("data-section=\"privacy\"", StringComparison.Ordinal), "Local-first privacy section is present.");

    await AssertMiddleFilterRemovalInBrowser(baseUrl, 1440, 900);
    await AssertMiddleFilterRemovalInBrowser(baseUrl, 390, 844);

    var visitorId = Guid.NewGuid();
    var sessionId = Guid.NewGuid();
    var analyticsResponse = await client.PostAsJsonAsync("/api/analytics/events", new
    {
        events = new[] { new { visitorId, sessionId, eventName = "page_view", occurredAtUtc = DateTimeOffset.UtcNow } }
    });
    Assert(analyticsResponse.StatusCode == HttpStatusCode.Accepted, "Analytics batch is accepted.");

    var invalidResponse = await client.PostAsJsonAsync("/api/early-access", new
    {
        email = "not-an-email", visitorId, sessionId, sourceSection = "test"
    });
    Assert(invalidResponse.StatusCode == HttpStatusCode.BadRequest, "Invalid email is rejected.");

    var email = $"smoke-{Guid.NewGuid():N}@example.com";
    var signup = new { email, visitorId, sessionId, sourceSection = "test" };
    var firstResponse = await client.PostAsJsonAsync("/api/early-access", signup);
    var firstBody = await firstResponse.Content.ReadAsStringAsync();
    Assert(firstResponse.IsSuccessStatusCode && firstBody.Contains("\"created\":true", StringComparison.Ordinal), "New signup is persisted.");
    var duplicateResponse = await client.PostAsJsonAsync("/api/early-access", signup);
    var duplicateBody = await duplicateResponse.Content.ReadAsStringAsync();
    Assert(duplicateResponse.IsSuccessStatusCode && duplicateBody.Contains("\"created\":false", StringComparison.Ordinal), "Duplicate signup is idempotent.");

    Console.WriteLine("All PeopleMap smoke tests passed.");
    return;
}
finally
{
    if (!process.HasExited) process.Kill(entireProcessTree: true);
    await process.WaitForExitAsync();
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException($"FAILED: {message}");
    Console.WriteLine($"PASS: {message}");
}

static async Task AssertMiddleFilterRemovalInBrowser(string baseUrl, int viewportWidth, int viewportHeight)
{
    Console.WriteLine($"Testing filter interactions at {viewportWidth}x{viewportHeight}.");
    var browserPath = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe")
    }.FirstOrDefault(File.Exists);
    Assert(browserPath is not null, "A Chromium browser is available for interaction regression tests.");

    var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var debugPort = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();

    var profileDirectory = Path.Combine(Path.GetTempPath(), $"peoplemap-smoke-{Guid.NewGuid():N}");
    Directory.CreateDirectory(profileDirectory);
    var browserStart = new ProcessStartInfo
    {
        FileName = browserPath,
        UseShellExecute = false,
        CreateNoWindow = true
    };
    browserStart.ArgumentList.Add("--headless=new");
    browserStart.ArgumentList.Add("--disable-gpu");
    browserStart.ArgumentList.Add("--no-sandbox");
    browserStart.ArgumentList.Add("--no-first-run");
    browserStart.ArgumentList.Add("--no-default-browser-check");
    browserStart.ArgumentList.Add($"--window-size={viewportWidth},{viewportHeight}");
    browserStart.ArgumentList.Add($"--remote-debugging-port={debugPort}");
    browserStart.ArgumentList.Add("--remote-allow-origins=*");
    browserStart.ArgumentList.Add($"--user-data-dir={profileDirectory}");
    browserStart.ArgumentList.Add(baseUrl);

    using var browser = Process.Start(browserStart) ?? throw new InvalidOperationException("Could not start Chromium browser.");
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(500) };
        string? webSocketUrl = null;
        for (var attempt = 0; attempt < 40 && webSocketUrl is null; attempt++)
        {
            try
            {
                using var targets = JsonDocument.Parse(await client.GetStringAsync($"http://127.0.0.1:{debugPort}/json/list"));
                webSocketUrl = targets.RootElement.EnumerateArray()
                    .FirstOrDefault(target => target.GetProperty("url").GetString()?.StartsWith(baseUrl, StringComparison.Ordinal) == true)
                    .GetProperty("webSocketDebuggerUrl").GetString();
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            catch (InvalidOperationException) { }
            await Task.Delay(250);
        }
        Assert(webSocketUrl is not null, "Browser opened the landing page.");

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(webSocketUrl!), CancellationToken.None);
        const string expression = """
            (async () => {
              let line;
              for (let attempt = 0; attempt < 50; attempt++) {
                line = document.querySelector('.filter-line');
                if (line?.querySelector('[data-operator]')?.dataset.operatorReady) break;
                await new Promise(resolve => setTimeout(resolve, 100));
              }
              const reset = document.querySelector('[data-reset-filter]');
              const add = line.querySelector('[data-add-filter]');
              const resultCount = document.querySelector('[data-result-count]');
              const countValue = () => Number.parseInt(resultCount.textContent, 10);
              const removeFilter = filter => line.querySelector(`[data-filter="${filter}"]`).click();
              const setLogic = (index, logic) => {
                const operator = line.querySelectorAll('[data-operator]')[index];
                while (operator.dataset.logic !== logic) operator.click();
              };
              const inspect = () => {
                const nodes = [...line.children].filter(node => node.matches('[data-filter], [data-operator]'));
                const kinds = nodes.map(node => node.hasAttribute('data-filter') ? 'filter' : 'operator');
                return {
                  sequence: nodes.map(node => node.hasAttribute('data-filter') ? node.dataset.filter : node.dataset.logic),
                  filterCount: kinds.filter(kind => kind === 'filter').length,
                  operatorCount: kinds.filter(kind => kind === 'operator').length,
                  adjacentOperators: kinds.some((kind, index) => kind === 'operator' && kinds[index + 1] === 'operator'),
                  structurallyValid: kinds.every((kind, index) => kind === (index % 2 === 0 ? 'filter' : 'operator')),
                  logicSynchronized: nodes.filter(node => node.hasAttribute('data-operator')).every(node => node.dataset.logic === node.textContent.trim())
                };
              };

              const trusted = line.querySelector('[data-filter="TRUSTED"]');
              trusted.previousElementSibling.click();
              const exactInitialSequence = inspect().sequence;
              const exactInitialCount = countValue();
              trusted.click();
              const middle = inspect();
              const middleRemoved = !line.querySelector('[data-filter="TRUSTED"]');
              const middleCount = countValue();

              reset.click();
              const resetCount = countValue();
              setLogic(0, 'OR');
              const fullExpressionOrCount = countValue();
              setLogic(0, 'NOT');
              const fullExpressionNotCount = countValue();

              reset.click();
              removeFilter('TRUSTED');
              removeFilter('FORMER');
              const workAndNewYork = countValue();
              setLogic(0, 'OR');
              const workOrNewYork = countValue();
              const previewRemainder = document.querySelector('.result-faces i')?.textContent;

              reset.click();
              removeFilter('NEW YORK');
              removeFilter('FORMER');
              const workAndTrusted = countValue();

              reset.click();
              removeFilter('TRUSTED');
              removeFilter('FORMER');
              setLogic(0, 'NOT');
              const workNotNewYork = countValue();

              reset.click();
              removeFilter('TRUSTED');
              const workAndNewYorkNotFormer = countValue();

              document.querySelector('[data-quick-filter="no-tags"]').click();
              const noTagsCount = countValue();
              reset.click();
              const resetAfterNoTagsCount = countValue();

              line.querySelector('[data-filter="WORK"]').click();
              const first = inspect();

              reset.click();
              line.querySelector('[data-filter="FORMER"]').click();
              const last = inspect();

              reset.click();
              line.querySelector('[data-filter="NEW YORK"]').click();
              line.querySelector('[data-filter="TRUSTED"]').click();
              const sequential = inspect();

              reset.click();
              line.querySelector('[data-filter="WORK"]').click();
              line.querySelector('[data-filter="NEW YORK"]').click();
              line.querySelector('[data-filter="TRUSTED"]').click();
              const single = inspect();
              line.querySelector('[data-filter="FORMER"]').click();
              const empty = inspect();
              add.click();
              const reAdded = inspect();
              const reAddedCount = countValue();

              reset.click();
              line.querySelector('[data-filter="TRUSTED"]').click();
              const operator = line.querySelector('[data-operator]');
              const cycle = [];
              for (let index = 0; index < 3; index++) {
                operator.click();
                cycle.push({ label: operator.textContent.trim(), logic: operator.dataset.logic });
              }
              operator.focus();
              return {
                middleRemoved,
                middle,
                exactInitialSequence,
                exactInitialCount,
                middleCount,
                resetCount,
                fullExpressionOrCount,
                fullExpressionNotCount,
                workAndNewYork,
                workOrNewYork,
                workAndTrusted,
                workNotNewYork,
                workAndNewYorkNotFormer,
                noTagsCount,
                resetAfterNoTagsCount,
                previewRemainder,
                firstValid: first.structurallyValid && first.filterCount === 3 && first.operatorCount === 2,
                lastValid: last.structurallyValid && last.filterCount === 3 && last.operatorCount === 2,
                sequentialValid: sequential.structurallyValid && sequential.filterCount === 2 && sequential.operatorCount === 1,
                singleValid: single.structurallyValid && single.filterCount === 1 && single.operatorCount === 0,
                emptyValid: empty.structurallyValid && empty.filterCount === 0 && empty.operatorCount === 0,
                reAddValid: reAdded.structurallyValid && reAdded.filterCount === 1 && reAdded.operatorCount === 0,
                reAddedCount,
                cycleValid: new Set(cycle.map(item => item.label)).size === 3 && cycle.every(item => item.label === item.logic),
                keyboardFocusable: document.activeElement === operator && operator instanceof HTMLButtonElement
              };
            })()
            """;
        var command = JsonSerializer.SerializeToUtf8Bytes(new
        {
            id = 1,
            method = "Runtime.evaluate",
            @params = new { expression, returnByValue = true, awaitPromise = true }
        });
        await socket.SendAsync(command, WebSocketMessageType.Text, true, CancellationToken.None);

        using var receiveTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var buffer = new byte[16 * 1024];
        JsonDocument? response = null;
        while (response is null)
        {
            using var responseBody = new MemoryStream();
            WebSocketReceiveResult receiveResult;
            do
            {
                receiveResult = await socket.ReceiveAsync(buffer, receiveTimeout.Token);
                responseBody.Write(buffer, 0, receiveResult.Count);
            } while (!receiveResult.EndOfMessage);

            var candidate = JsonDocument.Parse(responseBody.ToArray());
            if (candidate.RootElement.TryGetProperty("id", out var id) && id.GetInt32() == 1) response = candidate;
            else candidate.Dispose();
        }

        using (response)
        {
        var value = response.RootElement.GetProperty("result").GetProperty("result").GetProperty("value");
        Assert(value.GetProperty("middleRemoved").GetBoolean(), "Middle filter is removed from the expression DOM.");
        var middle = value.GetProperty("middle");
        AssertSequence(value.GetProperty("exactInitialSequence"), ["WORK", "AND", "NEW YORK", "OR", "TRUSTED", "NOT", "FORMER"], "Regression setup uses the exact four-condition expression.");
        AssertSequence(middle.GetProperty("sequence"), ["WORK", "AND", "NEW YORK", "NOT", "FORMER"], "Removing TRUSTED removes its preceding OR connector and preserves FORMER's NOT connector.");
        Assert(!middle.GetProperty("adjacentOperators").GetBoolean(), "Removing a middle filter leaves no adjacent operators.");
        Assert(middle.GetProperty("structurallyValid").GetBoolean(), "The remaining filter expression alternates chips and operators.");
        Assert(middle.GetProperty("logicSynchronized").GetBoolean(), "Operator labels and data-logic values remain synchronized.");
        Assert(value.GetProperty("exactInitialCount").GetInt32() == 9, "WORK AND NEW YORK OR TRUSTED NOT FORMER returns 9 people.");
        Assert(value.GetProperty("middleCount").GetInt32() == 4, "Removing middle TRUSTED recalculates WORK AND NEW YORK NOT FORMER to 4 people.");
        Assert(value.GetProperty("resetCount").GetInt32() == 3, "Reset restores the default expression count of 3 people.");
        Assert(value.GetProperty("fullExpressionOrCount").GetInt32() == 6, "WORK OR NEW YORK AND TRUSTED NOT FORMER returns 6 people.");
        Assert(value.GetProperty("fullExpressionNotCount").GetInt32() == 2, "WORK NOT NEW YORK AND TRUSTED NOT FORMER returns 2 people.");
        Assert(value.GetProperty("workAndNewYork").GetInt32() == 6, "WORK AND NEW YORK returns 6 people.");
        Assert(value.GetProperty("workOrNewYork").GetInt32() == 15, "WORK OR NEW YORK returns 15 people.");
        Assert(value.GetProperty("workAndTrusted").GetInt32() == 7, "WORK AND TRUSTED returns 7 people.");
        Assert(value.GetProperty("workNotNewYork").GetInt32() == 5, "WORK NOT NEW YORK returns 5 people.");
        Assert(value.GetProperty("workAndNewYorkNotFormer").GetInt32() == 4, "WORK AND NEW YORK NOT FORMER returns 4 people.");
        Assert(value.GetProperty("noTagsCount").GetInt32() == 3, "No tags returns the 3 untagged demo contacts.");
        Assert(value.GetProperty("resetAfterNoTagsCount").GetInt32() == 3, "Reset exits No tags and restores the expression result.");
        Assert(value.GetProperty("previewRemainder").GetString() == "+12", "Preview remainder equals count minus the three visible avatars.");
        Assert(value.GetProperty("firstValid").GetBoolean(), "Removing the first filter leaves a valid expression.");
        Assert(value.GetProperty("lastValid").GetBoolean(), "Removing the last filter leaves a valid expression.");
        Assert(value.GetProperty("sequentialValid").GetBoolean(), "Sequential removals leave a valid expression.");
        Assert(value.GetProperty("singleValid").GetBoolean(), "One remaining filter has no operators.");
        Assert(value.GetProperty("emptyValid").GetBoolean(), "Removing every filter leaves a clean empty state.");
        Assert(value.GetProperty("reAddValid").GetBoolean(), "Adding a filter after removal restores a valid expression.");
        Assert(value.GetProperty("reAddedCount").GetInt32() == 6, "Re-adding the removed FORMER filter recalculates to 6 people.");
        Assert(value.GetProperty("cycleValid").GetBoolean(), "Operator cycling keeps AND, OR, NOT labels and data-logic synchronized.");
        Assert(value.GetProperty("keyboardFocusable").GetBoolean(), "Filter operators remain native keyboard-focusable buttons.");
        }
    }
    finally
    {
        if (!browser.HasExited) browser.Kill(entireProcessTree: true);
        await browser.WaitForExitAsync();
        try { Directory.Delete(profileDirectory, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

static void AssertSequence(JsonElement actual, string[] expected, string message)
{
    var values = actual.EnumerateArray().Select(item => item.GetString()).ToArray();
    Assert(values.SequenceEqual(expected), $"{message} Actual: {string.Join(" ", values)}");
}
