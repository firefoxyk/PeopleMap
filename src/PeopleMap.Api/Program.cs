using System.ComponentModel.DataAnnotations;
using PeopleMap.Api.Models;
using PeopleMap.Api.Services;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
    WebRootPath = "wwwroot"
});
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
builder.Services.AddSingleton<IPrototypeStore, FilePrototypeStore>();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (context.File.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase))
        {
            context.Context.Response.Headers.CacheControl = "no-cache";
        }
    }
});

app.MapPost("/api/analytics/events", async (AnalyticsBatch batch, IPrototypeStore store, CancellationToken token) =>
{
    if (batch.Events.Count is 0 or > 100)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["events"] = ["Send between 1 and 100 events."] });
    }

    var allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        "page_view", "session_started", "section_viewed", "scroll_depth", "hero_cta_clicked",
        "feature_interaction", "early_access_clicked", "email_form_started", "email_form_submitted",
        "email_form_failed", "engagement_heartbeat"
    };
    if (batch.Events.Any(item => !allowed.Contains(item.EventName) || item.EventName.Length > 64))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["eventName"] = ["One or more event names are invalid."] });
    }

    await store.AppendEventsAsync(batch.Events, token);
    return Results.Accepted(value: new { accepted = batch.Events.Count });
});

app.MapPost("/api/early-access", async (EarlyAccessRequest request, IPrototypeStore store, CancellationToken token) =>
{
    var validator = new EmailAddressAttribute();
    if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 254 || !validator.IsValid(request.Email))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Enter a valid email address."] });
    }

    var result = await store.AddSignupAsync(request, token);
    return Results.Ok(new { id = result.Signup.Id, created = result.Created });
});

app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }));
app.MapFallbackToFile("index.html");
app.Run();

public partial class Program { }
