using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using IRIS.Core.Services.Contracts;
using IRIS.Core.Models;
using IRIS.UI.Helpers;
using IRIS.UI.Views.Shared;

namespace IRIS.UI.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private readonly IAuthenticationService _authService;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isLoading;
        private User? _currentUser;

        public LoginViewModel(IAuthenticationService authService)
        {
            _authService = authService;
            LoginCommand = new RelayCommand(async () => await LoginAsync(), CanLogin);
        }

        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged();
                (LoginCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged();
                (LoginCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public User? CurrentUser => _currentUser;

        public ICommand LoginCommand { get; }

        private bool CanLogin() => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password) && !IsLoading;

        private async Task LoginAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                var user = await _authService.AuthenticateAsync(Username, Password);

                if (user != null)
                {
                    _currentUser = user;

                    // Check if user must change password
                    if (user.MustChangePassword)
                    {
                        var app = (App)Application.Current;
                        var serviceProvider = app.GetServiceProvider();
                        var authService = serviceProvider.GetService<IAuthenticationService>();
                        
                        var changePasswordWindow = new ChangePasswordWindow(authService!, user);
                        var result = changePasswordWindow.ShowDialog();

                        if (result != true)
                        {
                            ErrorMessage = "Password change is required to continue.";
                            IsLoading = false;
                            return;
                        }
                    }

                    // Close login window and show main window
                    var loginWindow = Application.Current.Windows
                        .OfType<Window>()
                        .FirstOrDefault(w => w.DataContext == this);

                    if (loginWindow != null)
                    {
                        // Create and show main window using a scope
                        var app = (App)Application.Current;
                        var serviceProvider = app.GetServiceProvider();
                        
                        // Use IServiceScopeFactory to create a scope for the main window and its dependencies
                        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
                        var scope = scopeFactory.CreateScope();
                        
                        var mainWindow = scope.ServiceProvider.GetRequiredService<MainWindow>();
                        if (mainWindow != null)
                        {
                            mainWindow.Show();
                            // Set as main window
                            Application.Current.MainWindow = mainWindow;
                        }

                        // Close login window
                        loginWindow.Close();
                    }
                }
                else
                {
                    ErrorMessage = "Invalid username or password";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Login failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}