using CommunityToolkit.Mvvm.Input;

namespace Clario.ViewModels;

public partial class MoreViewModel : ViewModelBase
{
    public required ViewModelBase parentViewModel;

    [RelayCommand]
    private void GoToAnalytics()
    {
        if (parentViewModel is MainViewModel mainVm)
            mainVm.GoToAnalyticsCommand.Execute(null);
    }

    [RelayCommand]
    private void GoToBudget()
    {
        if (parentViewModel is MainViewModel mainVm)
            mainVm.GoToBudgetCommand.Execute(null);
    }

    [RelayCommand]
    private void GoToCategories()
    {
        if (parentViewModel is MainViewModel mainVm)
            mainVm.GoToCategoriesCommand.Execute(null);
    }
}
