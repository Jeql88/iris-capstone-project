namespace IRIS.UI.Services
{
    public interface INavigationAware
    {
        void OnNavigatedTo();
        void OnNavigatedFrom();
    }
}
