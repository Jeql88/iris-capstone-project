using System.Windows;
using System.Windows.Input;
using IRIS.Core.Services.Contracts;
using IRIS.UI.ViewModels;
using Wpf.Ui.Controls;

namespace IRIS.UI.Views.Shared
{
    public partial class LoginWindow : FluentWindow
    {
        public LoginWindow(IAuthenticationService authService)
        {
            InitializeComponent();

            DataContext = new LoginViewModel(authService);

            // Bind password box to view model
            if (PasswordBox != null)
            {
                PasswordBox.PasswordChanged += (s, e) =>
                {
                    if (DataContext is LoginViewModel vm)
                    {
                        vm.Password = PasswordBox.Password;
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