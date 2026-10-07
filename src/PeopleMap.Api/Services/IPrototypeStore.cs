using PeopleMap.Api.Models;

namespace PeopleMap.Api.Services;

public interface IPrototypeStore
{
    Task AppendEventsAsync(IEnumerable<AnalyticsEvent> events, CancellationToken cancellationToken);
    Task<(EarlyAccessSignup Signup, bool Created)> AddSignupAsync(EarlyAccessRequest request, CancellationToken cancellationToken);
}
