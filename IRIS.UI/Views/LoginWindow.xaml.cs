using System.Windows;
using System.Windows.Controls;
using IRIS.Core.Services;
using IRIS.UI.ViewModels;

namespace IRIS.UI.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();

            // Get authentication service from DI container or create instance
            var authService = new AuthenticationService(null!); // TODO: Inject proper context
            DataContext = new LoginViewModel(authService);

            // Bind password box to view model
            PasswordBox.PasswordChanged += (s, e) =>
            {
                if (DataContext is LoginViewModel vm)
                {
                    vm.Password = PasswordBox.Password;
                }
            };
        }
    }
}