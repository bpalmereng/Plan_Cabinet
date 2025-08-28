using Microsoft.Kiota.Abstractions.Authentication;


public class StaticAccessTokenProvider : IAccessTokenProvider
{
    private readonly string _accessToken;

    public StaticAccessTokenProvider(string accessToken)
    {
        _accessToken = accessToken;
    }

    // ✅ Exact match for the required method
    public Task<string> GetAuthorizationTokenAsync(
        Uri uri,
        Dictionary<string, object>? additionalAuthenticationContext = default,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_accessToken);
    }

    // ✅ Required concrete property, not interface
    public AllowedHostsValidator AllowedHostsValidator { get; } = new AllowedHostsValidator();
}