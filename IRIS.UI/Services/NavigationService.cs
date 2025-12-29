using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace IRIS.UI.Services
{
    public class NavigationService : INavigationService
    {
        private ContentControl? _navigationFrame;
        private IServiceProvider? _serviceProvider;
        private readonly Stack<(string viewKey, object? parameter)> _navigationStack = new();
        private readonly Dictionary<string, Type> _viewRegistry = new();

        public NavigationService()
        {
            RegisterViews();
        }

        public void Initialize(ContentControl navigationFrame, IServiceProvider serviceProvider)
        {
            _navigationFrame = navigationFrame;
            _serviceProvider = serviceProvider;
        }

        public bool CanGoBack => _navigationStack.Count > 1;

        private void RegisterViews()
        {
            _viewRegistry["Dashboard"] = typeof(Views.DashboardView);
            _viewRegistry["Monitor"] = typeof(Views.MonitorView);
            _viewRegistry["ViewScreen"] = typeof(Views.ViewScreenPage);
            _viewRegistry["PolicyEnforcement"] = typeof(Views.PolicyEnforcementView);
            _viewRegistry["SoftwareManagement"] = typeof(Views.SoftwareManagementView);
            _viewRegistry["UserManagement"] = typeof(Views.UserManagementView);
            _viewRegistry["Settings"] = typeof(Views.SettingsView);
        }

        public void NavigateTo(string viewKey, object? parameter = null)
        {
            if (_navigationFrame == null || _serviceProvider == null || !_viewRegistry.TryGetValue(viewKey, out var viewType))
                return;

            var view = _serviceProvider.GetService(viewType) ?? Activator.CreateInstance(viewType);
            
            if (view is UserControl userControl)
            {
                if (parameter != null && userControl is Views.ViewScreenPage viewScreenPage)
                {
                    viewScreenPage.LoadPCData((ViewModels.PCDisplayModel)parameter);
                }

                _navigationStack.Push((viewKey, parameter));
                _navigationFrame.Content = userControl;
            }
        }

        public void GoBack()
        {
            if (!CanGoBack || _navigationFrame == null || _serviceProvider == null) return;

            _navigationStack.Pop();
            var (viewKey, parameter) = _navigationStack.Peek();
            
            if (!_viewRegistry.TryGetValue(viewKey, out var viewType))
                return;

            var view = _serviceProvider.GetService(viewType) ?? Activator.CreateInstance(viewType);
            
            if (view is UserControl userControl)
            {
                _navigationFrame.Content = userControl;
            }
        }
    }
}
