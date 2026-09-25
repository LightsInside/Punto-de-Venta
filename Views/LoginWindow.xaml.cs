using System.Windows;
using System.Windows.Input;
using ZarelyPOS.Helpers;

namespace ZarelyPOS.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            TxtUsuario.Focus();
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            await IntentarLogin();
        }

        private async void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
                await IntentarLogin();
        }

        private async Task IntentarLogin()
        {
            var nombreUsuario = TxtUsuario.Text.Trim();
            var password = TxtPassword.Password;

            if (string.IsNullOrEmpty(nombreUsuario) || string.IsNullOrEmpty(password))
            {
                MostrarError("Por favor ingresa tu usuario y contraseña.");
                return;
            }

            BtnLogin.IsEnabled = false;
            BtnLogin.Content = "Verificando...";
            PanelError.Visibility = Visibility.Collapsed;

            var usuario = await App.Db.ValidarUsuarioAsync(nombreUsuario, password);

            if (usuario != null)
            {
                SessionManager.IniciarSesion(usuario);

                var dashboard = new DashboardWindow();
                dashboard.Show();
                this.Close();
            }
            else
            {
                MostrarError("Usuario o contraseña incorrectos. Verifica tus datos.");
                BtnLogin.IsEnabled = true;
                BtnLogin.Content = "Iniciar Sesion";
                TxtPassword.Clear();
                TxtPassword.Focus();
            }
        }

        private void MostrarError(string mensaje)
        {
            TxtError.Text = mensaje;
            PanelError.Visibility = Visibility.Visible;
        }
    }
}
