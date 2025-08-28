
using Microsoft.Graph.Sites.Item;
using Microsoft.Graph.Models;
using Microsoft.Graph.Sites;
using Microsoft.Kiota.Abstractions;

namespace Plan_Cabinet.Sharepoint
{
    public static class SitesRequestBuilderExtensions
    {
        public static async Task<Site?> GetByPathAsync(this SitesRequestBuilder sitesRequestBuilder, string sitePath, string hostname, IRequestAdapter requestAdapter)
        {
            if (string.IsNullOrEmpty(sitePath))
                throw new ArgumentNullException(nameof(sitePath));
            if (string.IsNullOrEmpty(hostname))
                throw new ArgumentNullException(nameof(hostname));
            if (requestAdapter == null)
                throw new ArgumentNullException(nameof(requestAdapter));

            var rawUrl = $"https://graph.microsoft.com/v1.0/sites/{hostname}:/sites/{sitePath}";

            var siteItemRequestBuilder = new SiteItemRequestBuilder(rawUrl, requestAdapter);

            return await siteItemRequestBuilder.GetAsync();
        }
    }
}