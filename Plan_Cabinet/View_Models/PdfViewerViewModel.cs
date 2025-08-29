using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plan_Cabinet.Helpers;
using Plan_Cabinet.Sharepoint;
using Syncfusion.Maui.PdfViewer;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;

namespace Plan_Cabinet.View_Models
{
    public partial class PdfViewerViewModel : ObservableObject, IDisposable
    {
        private readonly string _fileName;
        private bool _pdfIsLoaded = false;
        private CancellationTokenSource? _loadCancellationTokenSource;
        private CancellationTokenSource? _shareCancellationTokenSource;
        private readonly object _loadLock = new();
        private readonly GraphService _graphService;
        private MemoryStream? _pdfStream;
        private bool _isDisposed = false;

        [ObservableProperty]
        private ZoomMode _currentZoomMode = ZoomMode.FitToWidth;

        public PdfViewerViewModel(string fileName, string? clientId, string? tenantId, string? clientSecret, string? driveId)
        {            

            _fileName = fileName;
            // Pass the variables to the new constructor
            _graphService = new GraphService(clientId, tenantId, clientSecret, driveId);

            LoadPdfAsync();

            DownloadCommand = new AsyncRelayCommand(DownloadPdfAsync);
            ShareCommand = new RelayCommand(() => IsSharePopupVisible = true);
            CancelCommand = new AsyncRelayCommand(CancelAndPopAsync);
            ShareCancelCommand = new RelayCommand(OnShareCancel);
            ShareSendCommand = new AsyncRelayCommand(OnShareSend);
            ClearShareInputsCommand = new RelayCommand(ClearShareInputs);
        }
      

        [ObservableProperty]
        private bool isSharePopupVisible;

        [ObservableProperty]
        private string recipientNameEntryText = string.Empty;

        [ObservableProperty]
        private string recipientEmailEntryText = string.Empty;

        [ObservableProperty]
        private bool isNameTokensLayoutVisible;

        [ObservableProperty]
        private bool isEmailTokensLayoutVisible;

        [ObservableProperty]
        private bool allowDownloadPermission = true;

        [ObservableProperty]
        private MemoryStream? pdfStreamSource;

      

        public ObservableCollection<string> NameTokens { get; } = new();
        public ObservableCollection<string> EmailTokens { get; } = new();

        public ICommand DownloadCommand { get; }
        public ICommand ShareCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ShareCancelCommand { get; }
        public ICommand ShareSendCommand { get; }
        public ICommand ClearShareInputsCommand { get; }
        public string Title => Path.GetFileNameWithoutExtension(_fileName);

        // Events to communicate with the View for UI-specific tasks
        public event Action? PopRequested;
        public event Func<string, string, string, Task>? DisplayAlertRequested;
        public event Func<string, Task>? DisplayErrorAlertRequested;
        public event Func<string, Task>? DisplayDownloadSuccessAlertRequested;
        public event Func<string, Task>? OpenFileRequested;

        private async void LoadPdfAsync()
        {
            CancellationTokenSource? cts = null;
            CancellationToken ct;

            lock (_loadLock)
            {
                if (_pdfIsLoaded || _isDisposed)
                    return;

                _loadCancellationTokenSource?.Cancel();
                _loadCancellationTokenSource?.Dispose();

                _loadCancellationTokenSource = new CancellationTokenSource();
                cts = _loadCancellationTokenSource;
                ct = cts.Token;
            }

            try
            {
                await _graphService.InitializeAsync(ct);
                ct.ThrowIfCancellationRequested();

                var fileStream = await _graphService.DownloadFileAsStreamAsync(_fileName, ct);
                ct.ThrowIfCancellationRequested();

                if (fileStream == null)
                {
                    if (!ct.IsCancellationRequested && !_isDisposed)
                        await DisplayErrorAlertRequested?.Invoke("Unable to load PDF.");
                    return;
                }

                _pdfStream = await StreamHelper.ToSeekableMemoryStreamAsync(fileStream);
                _pdfStream.Position = 0;

                PdfStreamSource = _pdfStream;
                _pdfIsLoaded = true;
            }
            catch (OperationCanceledException)
            {
                // Expected cancellation - ignore
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested && !_isDisposed)
                {
                    Debug.WriteLine($"LoadPdf error: {ex}");
                    await DisplayErrorAlertRequested?.Invoke($"Failed to load PDF: {ex.Message}");
                }
            }
        }

        private void UnloadPdf()
        {
            if (_pdfIsLoaded)
            {
                PdfStreamSource = null;
                _pdfStream?.Dispose();
                _pdfStream = null;
                _pdfIsLoaded = false;
            }
        }

        public void OnDisappearing()
        {
            Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                _loadCancellationTokenSource?.Cancel();
                _loadCancellationTokenSource?.Dispose();
                _loadCancellationTokenSource = null;

                _shareCancellationTokenSource?.Cancel();
                _shareCancellationTokenSource?.Dispose();
                _shareCancellationTokenSource = null;

                UnloadPdf();
                NameTokens.Clear();
                EmailTokens.Clear();
            }
            _isDisposed = true;
        }

        private async Task CancelAndPopAsync()
        {
            _loadCancellationTokenSource?.Cancel();
            _loadCancellationTokenSource?.Dispose();
            _loadCancellationTokenSource = null;

            _isDisposed = true;

            UnloadPdf();
            PopRequested?.Invoke();
        }

        private async Task OnShareSend()
        {
            _shareCancellationTokenSource?.Cancel();
            _shareCancellationTokenSource?.Dispose();

            _shareCancellationTokenSource = new CancellationTokenSource();
            var ct = _shareCancellationTokenSource.Token;

            string lastName = RecipientNameEntryText?.Trim();
            if (!string.IsNullOrEmpty(lastName))
            {
                AddNameToken(lastName);
                RecipientNameEntryText = string.Empty;
            }

            string lastEmail = RecipientEmailEntryText?.Trim();
            if (!string.IsNullOrEmpty(lastEmail))
            {
                AddEmailToken(lastEmail);
                RecipientEmailEntryText = string.Empty;
            }

            var names = NameTokens.ToList();
            var emails = EmailTokens.ToList();

            if (!names.Any())
            {
                await DisplayErrorAlertRequested?.Invoke("Please enter at least one recipient name.");
                return;
            }

            if (!emails.Any())
            {
                await DisplayErrorAlertRequested?.Invoke("Please enter at least one email address.");
                return;
            }

            var invalidEmails = emails.Where(email => !IsValidEmail(email)).ToList();

            if (invalidEmails.Any())
            {
                await DisplayErrorAlertRequested?.Invoke($"These emails are invalid:\n{string.Join("\n", invalidEmails)}");
                return;
            }

            try
            {
                await _graphService.InitializeAsync(ct);
                ct.ThrowIfCancellationRequested();

                bool shared = await _graphService.ShareFileWithUsersAsync(_fileName, emails);

                if (shared)
                    await DisplayAlertRequested?.Invoke("Success", $"{_fileName} shared with: {string.Join(", ", emails)}", "OK");
                else
                    await DisplayAlertRequested?.Invoke("Failed", "Could not share the file.", "OK");
            }
            catch (OperationCanceledException)
            {
                // Sharing cancelled - no alert needed
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    await DisplayErrorAlertRequested?.Invoke($"Sharing failed: {ex.Message}");
            }
            finally
            {
                IsSharePopupVisible = false;
                ClearShareInputs();
            }
        }

        private void OnShareCancel()
        {
            _shareCancellationTokenSource?.Cancel();
            IsSharePopupVisible = false;
            ClearShareInputs();
        }

        private void ClearShareInputs()
        {
            NameTokens.Clear();
            EmailTokens.Clear();
            RecipientNameEntryText = string.Empty;
            RecipientEmailEntryText = string.Empty;
            AllowDownloadPermission = true;
            IsNameTokensLayoutVisible = false;
            IsEmailTokensLayoutVisible = false;
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private void AddNameToken(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            string capitalized = CapitalizeName(name);
            if (!NameTokens.Contains(capitalized))
            {
                NameTokens.Add(capitalized);
                IsNameTokensLayoutVisible = true;
            }
        }

        private void AddEmailToken(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return;
            if (!EmailTokens.Contains(email))
            {
                EmailTokens.Add(email);
                IsEmailTokensLayoutVisible = true;
            }
        }

        // Public methods called by code-behind to handle Entry events
        public void OnRecipientNameEntryCompleted()
        {
            string name = RecipientNameEntryText?.Trim();
            if (!string.IsNullOrEmpty(name))
            {
                AddNameToken(name);
                RecipientNameEntryText = string.Empty;
            }
        }

        public void OnRecipientEmailEntryCompleted()
        {
            string email = RecipientEmailEntryText?.Trim();
            if (!string.IsNullOrEmpty(email))
            {
                AddEmailToken(email);
                RecipientEmailEntryText = string.Empty;
            }
        }

        private string CapitalizeName(string input)
        {
            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
                parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1).ToLower();
            return string.Join(' ', parts);
        }

        private async Task DownloadPdfAsync()
        {
            try
            {
                Stream? sourceStream = null;

                // Check if the PDF is already in memory
                if (_pdfStream != null && _pdfStream.Length > 0)
                {
                    // Use the existing memory stream
                    _pdfStream.Position = 0; // Reset position before copying
                    sourceStream = _pdfStream;
                }
                else
                {
                    // If not in memory, download it
                    var ct = _loadCancellationTokenSource?.Token ?? CancellationToken.None;
                    await _graphService.InitializeAsync(ct);
                    sourceStream = await _graphService.DownloadFileAsStreamAsync(_fileName, ct);
                }

                if (sourceStream == null)
                {
                    await DisplayErrorAlertRequested?.Invoke("No PDF file available to save.");
                    return;
                }

                string downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                Directory.CreateDirectory(downloadsPath);

                var filePath = Path.Combine(downloadsPath, _fileName);

                using (var fileOutput = File.Create(filePath))
                {
                    await sourceStream.CopyToAsync(fileOutput);
                }

                // If the source was the main memory stream, we don't dispose it
                if (sourceStream != _pdfStream)
                {
                    sourceStream.Dispose();
                }

                await DisplayDownloadSuccessAlertRequested?.Invoke(filePath);
                await OpenFileRequested?.Invoke(filePath);
            }
            catch (Exception ex)
            {
                await DisplayErrorAlertRequested?.Invoke($"Failed to save or open PDF: {ex.Message}");
                Debug.WriteLine($"Download error: {ex}");
            }
        }
    }
}