using Plan_Cabinet.View_Models;
using Plan_Cabinet.Views;
using System.Diagnostics;

namespace Plan_Cabinet.Views
{
    public partial class View_Plans : ContentPage
    {
        private ViewPlansViewModel? _viewModel;

        public View_Plans()
        {
            InitializeComponent();
            //_viewModel = new ViewPlansViewModel();
            //BindingContext = _viewModel;
           
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            // Read environment variables here, as they are now available
            string? clientId = Environment.GetEnvironmentVariable("GRAPH_CLIENT_ID");
            string? tenantId = Environment.GetEnvironmentVariable("GRAPH_TENANT_ID");
            string? clientSecret = Environment.GetEnvironmentVariable("GRAPH_CLIENT_SECRET");
            string? driveId = Environment.GetEnvironmentVariable("GRAPH_DRIVE_ID");

            _viewModel = new ViewPlansViewModel(clientId, tenantId, clientSecret, driveId);
            BindingContext = _viewModel;

            // Subscribe to VM events here to ensure they are active when the page is shown
            _viewModel.ShowAlertRequested += OnShowAlertRequested;
            _viewModel.ShowAlertWithChoiceRequested += OnShowAlertWithChoiceRequested;
            _viewModel.NavigateToPdfRequested += OnNavigateToPdfRequested;
            _viewModel.ClosePageRequested += OnClosePageRequested;

            await _viewModel.InitializeAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Unsubscribe from events to prevent memory leaks
            _viewModel.ShowAlertRequested -= OnShowAlertRequested;
            _viewModel.ShowAlertWithChoiceRequested -= OnShowAlertWithChoiceRequested;
            _viewModel.NavigateToPdfRequested -= OnNavigateToPdfRequested;
            _viewModel.ClosePageRequested -= OnClosePageRequested;
        }
        protected override void OnHandlerChanging(HandlerChangingEventArgs args)
        {
            base.OnHandlerChanging(args);
            // Dispose the ViewModel when the page's handler is being removed
            if (args.NewHandler == null)
            {
                (_viewModel as IDisposable)?.Dispose();
                Debug.WriteLine("ViewPlansViewModel disposed.");
            }
        }

        // New private methods for the event handlers
        private async Task OnShowAlertRequested(string title, string message, string cancel)
        {
            await DisplayAlert(title, message, cancel);
        }

        private async Task<bool> OnShowAlertWithChoiceRequested(string title, string message, string accept, string cancel)
        {
            return await DisplayAlert(title, message, accept, cancel);
        }

        private async Task OnNavigateToPdfRequested(string filename)
        {
            await Navigation.PushAsync(new PdfViewerPage(filename));
        }

        private async Task OnClosePageRequested()
        {
            await Navigation.PopAsync();
        }
    }
}
