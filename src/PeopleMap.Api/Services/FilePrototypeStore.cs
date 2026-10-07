using System.Text.Json;
using PeopleMap.Api.Models;

namespace PeopleMap.Api.Services;

public sealed class FilePrototypeStore(IHostEnvironment environment) : IPrototypeStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _dataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");

    public async Task AppendEventsAsync(IEnumerable<AnalyticsEvent> events, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_dataDirectory);
        var lines = events.Select(item => JsonSerializer.Serialize(item with
        {
            EventId = item.EventId ?? Guid.NewGuid(),
            OccurredAtUtc = item.OccurredAtUtc == default ? DateTimeOffset.UtcNow : item.OccurredAtUtc
        }, JsonOptions));

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllLinesAsync(Path.Combine(_dataDirectory, "analytics.ndjson"), lines, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<(EarlyAccessSignup Signup, bool Created)> AddSignupAsync(EarlyAccessRequest request, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_dataDirectory);
        var path = Path.Combine(_dataDirectory, "early-access.ndjson");
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(path))
            {
                foreach (var line in await File.ReadAllLinesAsync(path, cancellationToken))
                {
                    var existing = JsonSerializer.Deserialize<EarlyAccessSignup>(line, JsonOptions);
                    if (existing is not null && string.Equals(existing.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
                    {
                        return (existing, false);
                    }
                }
            }

            var signup = new EarlyAccessSignup(
                Guid.NewGuid(), normalizedEmail, DateTimeOffset.UtcNow, request.VisitorId, request.SessionId,
                request.SourceSection, request.UtmSource, request.UtmMedium, request.UtmCampaign,
                request.ExperimentId, request.VariantId);
            await File.AppendAllTextAsync(path, JsonSerializer.Serialize(signup, JsonOptions) + Environment.NewLine, cancellationToken);
            return (signup, true);
        }
        finally
        {
            _gate.Release();
        }
    }
}
