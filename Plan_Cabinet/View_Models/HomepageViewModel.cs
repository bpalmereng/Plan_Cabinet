using Plan_Cabinet.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Plan_Cabinet.ViewModels
{
    public partial class HomePageViewModel : ObservableObject
    {
        private readonly INavigation _navigation;

        public IAsyncRelayCommand AddPlansCommand { get; }
        public IAsyncRelayCommand ViewPlansCommand { get; }

        public HomePageViewModel(INavigation navigation)
        {
            _navigation = navigation;
            AddPlansCommand = new AsyncRelayCommand(NavigateToAddPlansAsync);
            ViewPlansCommand = new AsyncRelayCommand(NavigateToViewPlansAsync);
        }

        private async Task NavigateToViewPlansAsync()
        {
            await _navigation.PushAsync(new View_Plans());
            
        }

        private async Task NavigateToAddPlansAsync()
        {
            await _navigation.PushAsync(new Add_Plans());
        }
    }
}