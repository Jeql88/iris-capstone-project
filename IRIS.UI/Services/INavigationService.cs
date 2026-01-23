namespace IRIS.UI.Services
{
    public interface INavigationService
    {
        void NavigateTo(string viewKey, object? parameter = null);
        void GoBack();
        bool CanGoBack { get; }
    }
}
