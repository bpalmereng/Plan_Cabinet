using Plan_Cabinet.View_Models;
using Syncfusion.Maui.PdfViewer;
using System.Diagnostics;

namespace Plan_Cabinet.Views
{
    public partial class PdfViewerPage : ContentPage
    {
        private readonly string _fileName;
        private PdfViewerViewModel? _viewModel;
        private CancellationTokenSource _cts;
        private bool _isDocumentLoaded = false;
        private bool _isSizeChanged = false;
        private bool _isZoomSet;

        public PdfViewerPage(string fileName)
        {
            InitializeComponent();
            _fileName = fileName;          
            
        }
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Read environment variables here, as they are now available
            string? clientId = Environment.GetEnvironmentVariable("GRAPH_CLIENT_ID");
            string? tenantId = Environment.GetEnvironmentVariable("GRAPH_TENANT_ID");
            string? clientSecret = Environment.GetEnvironmentVariable("GRAPH_CLIENT_SECRET");
            string? driveId = Environment.GetEnvironmentVariable("GRAPH_DRIVE_ID");

            // Initialize the ViewModel here with the environment variables
            _viewModel = new PdfViewerViewModel(_fileName, clientId, tenantId, clientSecret, driveId);
            BindingContext = _viewModel;

            // Subscribe to ViewModel events for UI-specific tasks
            _viewModel.PopRequested += OnPopRequested;
            _viewModel.DisplayAlertRequested += OnDisplayAlertRequested;
            _viewModel.DisplayErrorAlertRequested += OnDisplayErrorAlertRequested;
            _viewModel.DisplayDownloadSuccessAlertRequested += OnDisplayDownloadSuccessAlertRequested;
            _viewModel.OpenFileRequested += OnOpenFileRequested;       

            pdfViewer.DocumentLoaded += OnPdfViewerDocumentLoaded;
            pdfViewer.SizeChanged += OnPdfViewerSizeChanged;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (BindingContext is PdfViewerViewModel viewModel)
            {
                viewModel.PopRequested -= OnPopRequested;
                viewModel.DisplayAlertRequested -= OnDisplayAlertRequested;
                viewModel.DisplayErrorAlertRequested -= OnDisplayErrorAlertRequested;
                viewModel.DisplayDownloadSuccessAlertRequested -= OnDisplayDownloadSuccessAlertRequested;
                viewModel.OpenFileRequested -= OnOpenFileRequested;

                if (pdfViewer != null)
                {
                    pdfViewer.DocumentLoaded -= OnPdfViewerDocumentLoaded;
                    pdfViewer.SizeChanged -= OnPdfViewerSizeChanged;
                }
                viewModel.Dispose();
            }
        }

        private void OnRecipientNameEntryCompleted(object sender, EventArgs e)
        {
            if (BindingContext is PdfViewerViewModel viewModel)
            {
                viewModel.OnRecipientNameEntryCompleted();
            }
        }

        private void OnRecipientEmailEntryCompleted(object sender, EventArgs e)
        {
            if (BindingContext is PdfViewerViewModel viewModel)
            {
                viewModel.OnRecipientEmailEntryCompleted();
            }
        }

        private async void OnPopRequested()
        {
            await Navigation.PopAsync();
        }

        private async Task OnDisplayAlertRequested(string title, string message, string buttonText)
        {
            await DisplayAlert(title, message, buttonText);
        }

        private async Task OnDisplayErrorAlertRequested(string message)
        {
            await DisplayAlert("Error", message, "OK");
        }

        private async Task OnDisplayDownloadSuccessAlertRequested(string filePath)
        {
            await DisplayAlert("Success", $"PDF saved to:\n{filePath}", "OK");
        }

        private async Task OnOpenFileRequested(string filePath)
        {
            await Launcher.Default.OpenAsync(new OpenFileRequest
            {
                File = new ReadOnlyFile(filePath)
            });
        }
        private void OnPdfViewerDocumentLoaded(object sender, EventArgs e)
        {
            _isDocumentLoaded = true;
            _ = TrySetZoomMode();
        }

        private void OnPdfViewerSizeChanged(object sender, EventArgs e)
        {
            _isSizeChanged = true;
            _ = TrySetZoomMode();
        }
       
        private async Task TrySetZoomMode()
        {
            if (_isDocumentLoaded && _isSizeChanged && !_isZoomSet)
            {
                // Add a small delay to ensure the UI has time to fully render.
                await Task.Delay(100);
                pdfViewer.ZoomMode = ZoomMode.FitToWidth;
                _isZoomSet = true;
            }
        }

    }
}