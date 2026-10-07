namespace PeopleMap.Api.Models;

public sealed record AnalyticsEvent(
    Guid? EventId,
    Guid VisitorId,
    Guid SessionId,
    string EventName,
    DateTimeOffset OccurredAtUtc,
    string? Page,
    string? Section,
    string? Element,
    string? Value,
    string? Referrer,
    string? LandingUrl,
    string? UtmSource,
    string? UtmMedium,
    string? UtmCampaign,
    string? UtmContent,
    string? UtmTerm,
    string? Country,
    string? Language,
    string? DeviceType,
    string? OS,
    string? Browser,
    int? ScreenWidth,
    int? ScreenHeight,
    string? ExperimentId,
    string? VariantId);

public sealed record AnalyticsBatch(IReadOnlyList<AnalyticsEvent> Events);
