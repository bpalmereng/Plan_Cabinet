
using Microsoft.Graph;
using Microsoft.Graph.Drives.Item.Items.Item.Invite;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;
using System.Diagnostics;


namespace Plan_Cabinet.Sharepoint
{
    public class GraphService: IDisposable
    {
        private readonly string _clientId = Environment.GetEnvironmentVariable("GRAPH_CLIENT_ID");
        private readonly string _tenantId = Environment.GetEnvironmentVariable("GRAPH_TENANT_ID");
        private readonly string _clientSecret = Environment.GetEnvironmentVariable("GRAPH_CLIENT_SECRET");
        private readonly string _driveId = Environment.GetEnvironmentVariable("GRAPH_DRIVE_ID");
        private static string folderPath = "Plan Cabinet";
        public string? AccessToken { get; private set; }

        private GraphServiceClient? _graphClient;

        public async Task InitializeAsync(CancellationToken ct)
        {
            // No 'using' statement here
            var confidentialClient = ConfidentialClientApplicationBuilder
                .Create(_clientId)
                .WithTenantId(_tenantId)
                .WithClientSecret(_clientSecret)
                .Build();

            var authResult = await confidentialClient
                .AcquireTokenForClient(new[] { "https://graph.microsoft.com/.default" })
                .ExecuteAsync(ct);

            ct.ThrowIfCancellationRequested();

            AccessToken = authResult.AccessToken;

            var authProvider = new AppOnlyAuthProvider(authResult.AccessToken);
            _graphClient = new GraphServiceClient(authProvider);
        }


        public async Task<Stream?> DownloadFileAsStreamAsync(string fileName, CancellationToken ct)
        {
            if (_graphClient == null)
                throw new InvalidOperationException("Graph client not initialized.");

            string fullPath = $"{folderPath}/{fileName}";

            try
            {
                var stream = await _graphClient.Drives[_driveId]
                    .Root
                    .ItemWithPath(fullPath)
                    .Content
                    .GetAsync(null, ct);

                return stream;
            }
            catch (ServiceException ex) when (ex.ResponseStatusCode == 404)
            {
                Debug.WriteLine($"File '{fileName}' not found at path '{fullPath}'.");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error downloading file '{fileName}': {ex}");
                return null;
            }
        }
        public async Task UploadFileAsync(string planName, byte[] fileBytes)
        {
            if (_graphClient == null)
                throw new InvalidOperationException("Graph client not initialized.");

            try
            {
                var fileName = $"{planName}.pdf";
                var fullPath = $"{folderPath}/{fileName}"; // "Plan Cabinet/Test.pdf"

                using var stream = new MemoryStream(fileBytes);
                stream.Position = 0;

                var uploadedItem = await _graphClient.Drives[_driveId]
                    .Root
                    .ItemWithPath(fullPath)
                    .Content
                    .PutAsync(stream);

                Debug.WriteLine($"File uploaded successfully: {uploadedItem?.Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error uploading file '{planName}.pdf': {ex}");
                throw;
            }
        }
        public async Task<bool> DeleteFileIfExistsAsync(string fileName)
        {
            if (_graphClient == null)
                throw new InvalidOperationException("Graph client not initialized.");

            var fullPath = $"{folderPath}/{fileName}";

            try
            {
                // Try to get the file's DriveItem by path
                var driveItem = await _graphClient.Drives[_driveId]
                    .Root
                    .ItemWithPath(fullPath)
                    .GetAsync();

                if (driveItem != null)
                {
                    // File exists – delete it
                    await _graphClient.Drives[_driveId]
                        .Items[driveItem.Id]
                        .DeleteAsync();

                    Debug.WriteLine($"File '{fileName}' successfully deleted from SharePoint.");
                    return true;
                }
            }
            catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
            {
                if (ex.Error?.Code == "itemNotFound")
                {
                    Debug.WriteLine($"File '{fileName}' not found. Nothing to delete.");
                    return false;
                }
                Debug.WriteLine($"Graph ODataError deleting file '{fileName}': {ex.Error?.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected error deleting file '{fileName}': {ex}");
            }

            return false;
        }
        public async Task<bool> FileExistsAsync(string fileName)
        {
            if (_graphClient == null)
                throw new InvalidOperationException("Graph client not initialized.");

            var fullPath = $"{folderPath}/{fileName}";

            try
            {
                // Try to get the file by path in the folder
                var driveItem = await _graphClient.Drives[_driveId]
                    .Root
                    .ItemWithPath(fullPath)
                    .GetAsync();

                // If no exception, file exists
                return driveItem != null;
            }
            catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
            {
                if (ex.Error?.Code == "itemNotFound")
                {
                    // File not found, so does NOT exist
                    return false;
                }

                // Other error, throw or handle differently
                throw;
            }
            catch (Exception)
            {
                // Unknown error, consider file not existing or rethrow
                return false;
            }
        }

        public async Task<bool> RenameFileAsync(string oldFileName, string newFileName)
        {
            if (_graphClient == null)
                throw new InvalidOperationException("Graph client not initialized.");

            string oldPath = $"{folderPath}/{oldFileName}";
            string newPath = $"{folderPath}/{newFileName}";

            try
            {
                // Get the file by its current path
                var driveItem = await _graphClient.Drives[_driveId]
                    .Root
                    .ItemWithPath(oldPath)
                    .GetAsync();

                if (driveItem == null)
                {
                    Debug.WriteLine($"Cannot rename. File '{oldFileName}' not found.");
                    return false;
                }

                var updateItem = new DriveItem
                {
                    Name = newFileName
                };

                await _graphClient.Drives[_driveId]
                    .Items[driveItem.Id]
                    .PatchAsync(updateItem);

                Debug.WriteLine($"File renamed from '{oldFileName}' to '{newFileName}'.");
                return true;
            }
            catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
            {
                if (ex.Error?.Code == "itemNotFound")
                {
                    Debug.WriteLine($"Rename failed: File '{oldFileName}' not found.");
                }
                else
                {
                    Debug.WriteLine($"Graph ODataError during rename: {ex.Error?.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected error renaming file from '{oldFileName}' to '{newFileName}': {ex}");
            }

            return false;
        }
                
        public async Task<bool> ShareFileWithUsersAsync(string fileName, List<string> recipientEmails)
        {
            if (_graphClient == null)
                throw new InvalidOperationException("Graph client not initialized.");

            var fullPath = $"{folderPath}/{fileName}";

            try
            {
                var driveItem = await _graphClient.Drives[_driveId]
                    .Root
                    .ItemWithPath(fullPath)
                    .GetAsync();

                if (driveItem == null)
                    return false;

                var recipients = recipientEmails.Select(email => new DriveRecipient { Email = email }).ToList();

                var inviteBody = new InvitePostRequestBody
                {
                    Recipients = recipients,
                    Message = "Here's the PDF file.",
                    RequireSignIn = true,
                    SendInvitation = true,
                    Roles = new List<string> { "read" } // or "write"
                };

                var result = await _graphClient.Drives[_driveId]
                    .Items[driveItem.Id]
                    .Invite
                    .PostAsInvitePostResponseAsync(inviteBody);

                return result?.Value?.Count > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sharing file: {ex}");
                return false;
            }
        }
        public void Dispose()
        {
            // Dispose the GraphServiceClient
            _graphClient?.Dispose();
            // Suppress finalization to prevent the GC from calling the finalizer
            GC.SuppressFinalize(this);
        }
    }

}