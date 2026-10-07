using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;

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
