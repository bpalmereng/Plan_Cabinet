using Plan_Cabinet.ViewModels;
using System.Diagnostics;

namespace Plan_Cabinet;

public partial class HomePage : ContentPage
{
    private readonly HomePageViewModel _viewModel;

    public HomePage()
    {
        InitializeComponent();
        _viewModel = new HomePageViewModel(Navigation);
        BindingContext = _viewModel;

        AttachGestures(); // Handles animation triggers separately
    }

    private void AttachGestures()
    {
        var addGesture = new TapGestureRecognizer();
        addGesture.Tapped += OnAddPlanTapped;
        Add_Plan_Layout.GestureRecognizers.Add(addGesture);

        var viewGesture = new TapGestureRecognizer();
        viewGesture.Tapped += OnViewPlanTapped;
        View_Plan_Layout.GestureRecognizers.Add(viewGesture);
    }

    private async void OnAddPlanTapped(object? sender, EventArgs e)
    {
        await AnimateAndNavigate(() => _viewModel.AddPlansCommand.ExecuteAsync(null), AddIconBlack, AddIconBlue);
    }

    private async void OnViewPlanTapped(object? sender, EventArgs e)
    {
        await AnimateAndNavigate(() => _viewModel.ViewPlansCommand.ExecuteAsync(null), ViewIconBlack, ViewIconBlue);
    }

    private async Task AnimateAndNavigate(Func<Task> navigateFunc, VisualElement blackIcon, VisualElement blueIcon)
    {
        try
        {
            await Task.WhenAll(
                blackIcon.FadeTo(0, 400),
                blackIcon.ScaleTo(1.25, 100),
                blueIcon.ScaleTo(1.25, 100),
                blueIcon.FadeTo(1, 400)
            );

            await Task.WhenAll(
                blackIcon.FadeTo(1, 400),
                blackIcon.ScaleTo(1, 100),
                blueIcon.ScaleTo(1, 100),
                blueIcon.FadeTo(0, 400)
            );

            await Task.WhenAll(
                IsBusyIndicator.FadeTo(1, 350),
                isbusytext.FadeTo(1, 350),
                Task.Delay(100)
            );

            await navigateFunc();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Navigation failed: {ex}");
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        NavigationPage.SetHasNavigationBar(this, false);
        Title = string.Empty;
        IsBusyIndicator.Opacity = 0;
        isbusytext.Opacity = 0;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        IsBusyIndicator.Opacity = 0;
        isbusytext.Opacity = 0;
    }
}
