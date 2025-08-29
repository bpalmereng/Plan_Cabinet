using Plan_Cabinet.View_Models;
using System.Diagnostics;

namespace Plan_Cabinet.Views;

public partial class Add_Plans : ContentPage
{
    // Make the ViewModel field nullable to resolve the CS8618 warning.
    private AddPlansViewModel? _viewModel;
    public Add_Plans()
    {
        InitializeComponent();

        NavigationPage.SetHasNavigationBar(this, false);      

        Init(); // wire up UI-specific behavior (focus, gestures)
      
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Read environment variables here, as they are now available
        string? clientId = Environment.GetEnvironmentVariable("GRAPH_CLIENT_ID");
        string? tenantId = Environment.GetEnvironmentVariable("GRAPH_TENANT_ID");
        string? clientSecret = Environment.GetEnvironmentVariable("GRAPH_CLIENT_SECRET");
        string? driveId = Environment.GetEnvironmentVariable("GRAPH_DRIVE_ID");

        // Assign the ViewModel to a field and pass the environment variables
        _viewModel = new AddPlansViewModel(this.Navigation, clientId, tenantId, clientSecret, driveId);
        BindingContext = _viewModel;

        // Subscribe to ViewModel events to handle UI interactions
        _viewModel.ShowAlertRequested += OnShowAlertRequested;
        _viewModel.ShowAlertWithChoiceRequested += OnShowAlertWithChoiceRequested;
        _viewModel.ClosePageRequested += OnClosePageRequested;
       
    }
    void Init()
    {
        // Focus order like original
        Plan_Name_Entry.Focus();

        // Use named methods for event handlers to allow for proper unsubscription
        Rd_Name_Entry.Completed += OnRdNameEntryCompleted;
        Subdivision_Entry.Completed += OnSubdivisionEntryCompleted;
        Reference_Entry.Completed += OnReferenceEntryCompleted;

        // Use the named method to subscribe to the event
        Plan_Name_Entry.Completed += OnPlanNameEntryCompleted;
    }

    // Named event handler methods to prevent memory leaks
    private void OnRdNameEntryCompleted(object? sender, EventArgs e) => Subdivision_Entry.Focus();
    private void OnSubdivisionEntryCompleted(object? sender, EventArgs e) => Reference_Entry.Focus();
    private void OnReferenceEntryCompleted(object? sender, EventArgs e) => Date_Picker.Focus();


    // Corrected method signature to use nullable object?
    private async void OnPlanNameEntryCompleted(object? sender, EventArgs e)
    {
        if (_viewModel.ValidatePlanNameCommand.CanExecute(null))
        {
            await _viewModel.ValidatePlanNameCommand.ExecuteAsync(null);
        }
    }

    // Unsubscribe from events to prevent memory leaks
    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Use the named methods to unsubscribe
        Rd_Name_Entry.Completed -= OnRdNameEntryCompleted;
        Subdivision_Entry.Completed -= OnSubdivisionEntryCompleted;
        Reference_Entry.Completed -= OnReferenceEntryCompleted;

        // Use the named method to unsubscribe from the event
        Plan_Name_Entry.Completed -= OnPlanNameEntryCompleted;

        // Unsubscribe from ViewModel events
        _viewModel.ShowAlertRequested -= OnShowAlertRequested;
        _viewModel.ShowAlertWithChoiceRequested -= OnShowAlertWithChoiceRequested;
        _viewModel.ClosePageRequested -= OnClosePageRequested;
    }

    // Private methods for the event handlers
    private async Task OnShowAlertRequested(string title, string message, string cancel)
    {
        await DisplayAlert(title, message, cancel);
    }

    private async Task<bool> OnShowAlertWithChoiceRequested(string title, string message, string accept, string cancel)
    {
        return await DisplayAlert(title, message, accept, cancel);
    }

    private async void OnClosePageRequested()
    {
        await Navigation.PopAsync();
    }

    // Dispose the ViewModel when the page's handler is being removed
    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);
        // Dispose the ViewModel when the page's handler is being removed
        if (args.NewHandler == null)
        {
            (_viewModel as IDisposable)?.Dispose();
            Debug.WriteLine("AddPlansViewModel disposed.");
        }
    }
}