using System.Windows.Controls;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IRIS.UI.Services
{
    public class NavigationService : INavigationService
    {
        private ContentControl? _navigationFrame;
        private IServiceProvider? _serviceProvider;
        private IServiceScope? _currentScope;
        private readonly Stack<(string viewKey, object? parameter)> _navigationStack = new();
        private readonly Dictionary<string, Type> _viewRegistry = new();
        private ILogger<NavigationService>? _logger;

        public NavigationService()
        {
            RegisterViews();
        }

        public void Initialize(ContentControl navigationFrame, IServiceProvider serviceProvider)
        {
            _navigationFrame = navigationFrame;
            _serviceProvider = serviceProvider;
            _logger = serviceProvider.GetService<ILogger<NavigationService>>();
        }

        public bool CanGoBack => _navigationStack.Count > 1;

        private void RegisterViews()
        {
            // Common Views (all roles)
            _viewRegistry["Dashboard"] = typeof(Views.Common.DashboardView);
            _viewRegistry["Settings"] = typeof(Views.Common.SettingsView);
            _viewRegistry["AccessLogs"] = typeof(Views.Common.AccessLogsView);
            _viewRegistry["UsageMetrics"] = typeof(Views.Common.UsageMetricsView);
            _viewRegistry["Alerts"] = typeof(Views.Common.AlertsView);

            // Admin Views
            _viewRegistry["UserManagement"] = typeof(Views.Admin.UserManagementView);
            _viewRegistry["PolicyEnforcement"] = typeof(Views.Admin.PolicyEnforcementView);
            _viewRegistry["Labs"] = typeof(Views.Admin.LabsView);

            // Personnel Views
            _viewRegistry["Monitor"] = typeof(Views.Personnel.MonitorView);
            _viewRegistry["FileManagement"] = typeof(Views.Personnel.FileManagementView);

            // Faculty Views
            _viewRegistry["ViewScreen"] = typeof(Views.Faculty.ViewScreenPage);
        }

        public void NavigateTo(string viewKey, object? parameter = null)
        {
            if (_navigationFrame == null || _serviceProvider == null || !_viewRegistry.TryGetValue(viewKey, out var viewType))
                return;

            try
            {
                NotifyCurrentViewNavigatedFrom();
                _navigationFrame.Content = null;
                DisposeCurrentScopeSafe();
                _currentScope = _serviceProvider.CreateScope();
                
                var view = _currentScope.ServiceProvider.GetRequiredService(viewType);
                
                if (view is UserControl userControl)
                {
                    if (parameter != null && userControl is Views.Faculty.ViewScreenPage viewScreenPage)
                    {
                        viewScreenPage.LoadPCData((ViewModels.PCDisplayModel)parameter);
                    }

                    _navigationStack.Push((viewKey, parameter));
                    _navigationFrame.Content = userControl;
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"[NavigationService] Failed to navigate to '{viewKey}'.\n" +
                               $"  Exception: {ex.GetType().FullName}\n" +
                               $"  Message: {ex.Message}\n" +
                               $"  Inner: {ex.InnerException?.Message}\n" +
                               $"  StackTrace:\n{ex.StackTrace}";
                System.Diagnostics.Debug.WriteLine(errorMsg);
                _logger?.LogError(ex, "Navigation error to '{ViewKey}'", viewKey);
                System.Windows.MessageBox.Show(
                    $"Failed to navigate to {viewKey}:\n\n{ex.Message}\n\nInner: {ex.InnerException?.Message}",
                    "Navigation Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        public void GoBack()
        {
            if (!CanGoBack || _navigationFrame == null || _serviceProvider == null) return;

            _navigationStack.Pop();
            var (viewKey, parameter) = _navigationStack.Peek();

            if (!_viewRegistry.TryGetValue(viewKey, out var viewType))
                return;

            try
            {
                NotifyCurrentViewNavigatedFrom();
                _navigationFrame.Content = null;
                DisposeCurrentScopeSafe();
                _currentScope = _serviceProvider.CreateScope();
                
                var view = _currentScope.ServiceProvider.GetRequiredService(viewType);
                
                if (view is UserControl userControl)
                {
                    if (parameter != null && userControl is Views.Faculty.ViewScreenPage viewScreenPage)
                    {
                        viewScreenPage.LoadPCData((ViewModels.PCDisplayModel)parameter);
                    }

                    _navigationFrame.Content = userControl;
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"[NavigationService] Failed to go back to '{viewKey}'.\n" +
                               $"  Exception: {ex.GetType().FullName}\n" +
                               $"  Message: {ex.Message}\n" +
                               $"  Inner: {ex.InnerException?.Message}\n" +
                               $"  StackTrace:\n{ex.StackTrace}";
                System.Diagnostics.Debug.WriteLine(errorMsg);
                _logger?.LogError(ex, "Navigation error (back) to '{ViewKey}'", viewKey);
            }
        }

        private void NotifyCurrentViewNavigatedFrom()
        {
            if (_navigationFrame?.Content is not FrameworkElement currentView)
            {
                return;
            }

            try
            {
                if (currentView.DataContext is INavigationAware navigationAware)
                {
                    navigationAware.OnNavigatedFrom();
                }

                if (currentView is INavigationAware viewNavigationAware)
                {
                    viewNavigationAware.OnNavigatedFrom();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error while notifying current view of navigation away.");
            }
        }

        private void DisposeCurrentScopeSafe()
        {
            var scopeToDispose = _currentScope;
            _currentScope = null;

            if (scopeToDispose == null)
            {
                return;
            }

            try
            {
                scopeToDispose.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Scope was already disposed; safe to ignore.
            }
            catch (NullReferenceException ex)
            {
                _logger?.LogWarning(ex, "Null reference while disposing previous navigation scope; continuing navigation.");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error disposing previous navigation scope; continuing navigation.");
            }
        }
    }
}
