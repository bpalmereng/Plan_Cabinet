using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Plan_Cabinet.Helpers
{
    public class BindableSearchBar : SearchBar
    {
        public static readonly BindableProperty SearchButtonPressedCommandProperty =
            BindableProperty.Create(
                nameof(SearchButtonPressedCommand),
                typeof(ICommand),
                typeof(BindableSearchBar),
                null);

        public ICommand SearchButtonPressedCommand
        {
            get => (ICommand)GetValue(SearchButtonPressedCommandProperty);
            set => SetValue(SearchButtonPressedCommandProperty, value);
        }

        public BindableSearchBar()
        {
            this.SearchButtonPressed += (s, e) =>
            {
                if (SearchButtonPressedCommand?.CanExecute(null) == true)
                {
                    SearchButtonPressedCommand.Execute(null);
                }
            };
        }
    }
}