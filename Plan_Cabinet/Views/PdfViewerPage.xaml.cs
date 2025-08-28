using Plan_Cabinet.View_Models;
using Syncfusion.Maui.PdfViewer;
using System.Diagnostics;

namespace Plan_Cabinet.Views
{
    public partial class PdfViewerPage : ContentPage
    {
        private CancellationTokenSource _cts;
        private bool _isDocumentLoaded = false;
        private bool _isSizeChanged = false;
        private bool _isZoomSet;

        public PdfViewerPage(string fileName)
        {
            InitializeComponent();
            var viewModel = new PdfViewerViewModel(fileName);
            BindingContext = viewModel;

            // Subscribe to ViewModel events for UI-specific tasks
            viewModel.PopRequested += OnPopRequested;
            viewModel.DisplayAlertRequested += OnDisplayAlertRequested;
            viewModel.DisplayErrorAlertRequested += OnDisplayErrorAlertRequested;
            viewModel.DisplayDownloadSuccessAlertRequested += OnDisplayDownloadSuccessAlertRequested;
            viewModel.OpenFileRequested += OnOpenFileRequested;
            
        }
        protected override void OnAppearing()
        {
            base.OnAppearing();
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