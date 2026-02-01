using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IRIS.Core.Services;
using IRIS.UI.ViewModels;

namespace IRIS.UI.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow(IAuthenticationService authService)
        {
            InitializeComponent();

            DataContext = new LoginViewModel(authService);

            // Bind password box to view model
            var passwordBox = (PasswordBox)FindName("PasswordBox");
            if (passwordBox != null)
            {
                passwordBox.PasswordChanged += (s, e) =>
                {
                    if (DataContext is LoginViewModel vm)
                    {
                        vm.Password = passwordBox.Password;
                    }
                };
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is LoginViewModel vm)
            {
                if (vm.LoginCommand.CanExecute(null))
                {
                    vm.LoginCommand.Execute(null);
                }
            }
        }
    }
}