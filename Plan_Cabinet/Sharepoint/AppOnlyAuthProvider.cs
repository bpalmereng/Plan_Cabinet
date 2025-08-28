using Microsoft.Kiota.Abstractions.Authentication;

namespace Plan_Cabinet.Sharepoint
{
    public class AppOnlyAuthProvider : BaseBearerTokenAuthenticationProvider
    {
        public AppOnlyAuthProvider(string accessToken)
            : base(new StaticAccessTokenProvider(accessToken))
        {
        }
    }
}