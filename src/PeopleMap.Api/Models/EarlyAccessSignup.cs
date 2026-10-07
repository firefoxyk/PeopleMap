namespace PeopleMap.Api.Models;

public sealed record EarlyAccessRequest(
    string Email,
    Guid VisitorId,
    Guid SessionId,
    string SourceSection,
    string? UtmSource,
    string? UtmMedium,
    string? UtmCampaign,
    string? ExperimentId,
    string? VariantId);

public sealed record EarlyAccessSignup(
    Guid Id,
    string Email,
    DateTimeOffset CreatedAtUtc,
    Guid VisitorId,
    Guid SessionId,
    string SourceSection,
    string? UtmSource,
    string? UtmMedium,
    string? UtmCampaign,
    string? ExperimentId,
    string? VariantId);
