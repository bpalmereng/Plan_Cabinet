using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Abstractions;


namespace Plan_Cabinet.Sharepoint
{
    public class AuthProvider : IAuthenticationProvider
    {
        private readonly string _accessToken;

        public AuthProvider(string accessToken)
        {
            _accessToken = accessToken;
        }

        public Task AuthenticateRequestAsync(
            RequestInformation request,
            Dictionary<string, object> additionalAuthenticationContext = null,
            CancellationToken cancellationToken = default)
        {
            request.Headers["Authorization"] = new[] { $"Bearer {_accessToken}" };
            return Task.CompletedTask;
        }
    }
}
