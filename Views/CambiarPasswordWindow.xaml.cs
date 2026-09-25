using System.Windows;
using ZarelyPOS.Models;

namespace ZarelyPOS.Views
{
    public partial class CambiarPasswordWindow : Window
    {
        private readonly Usuario _usuario;
        private readonly bool _modoAdmin;

        /// <summary>
        /// Contiene la nueva contrasena solo si el usuario hizo clic en Guardar
        /// y todas las validaciones pasaron.
        /// </summary>
        public string NuevaPassword { get; private set; } = string.Empty;

        /// <param name="usuario">Usuario al que se le va a cambiar la contrasena.</param>
        /// <param name="modoAdmin">
        /// true  = el admin esta restableciendo la contrasena de otro usuario
        ///         (no se pide la contrasena actual).
        /// false = el propio usuario quiere cambiar su contrasena
        ///         (se le pide la actual para verificar identidad).
        /// </param>
        public CambiarPasswordWindow(Usuario usuario, bool modoAdmin)
        {
            InitializeComponent();
            _usuario   = usuario;
            _modoAdmin = modoAdmin;

            ConfigurarSegunModo();
        }

        private void ConfigurarSegunModo()
        {
            if (_modoAdmin)
            {
                TxtTitulo.Text    = "Restablecer Contraseña";
                TxtSubtitulo.Text = $"Asignar una nueva contraseña para '{_usuario.NombreCompleto}' " +
                                    $"(usuario: {_usuario.NombreUsuario}).";
                PanelActual.Visibility = Visibility.Collapsed;
                FNueva.Focus();
            }
            else
            {
                TxtTitulo.Text    = "Cambiar mi contraseña";
                TxtSubtitulo.Text = "Por seguridad, ingresa tu contraseña actual y luego la nueva.";
                PanelActual.Visibility = Visibility.Visible;
                FActual.Focus();
            }
        }

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            PanelError.Visibility = Visibility.Collapsed;

            var nueva     = FNueva.Password;
            var confirmar = FConfirmar.Password;

            // Validaciones comunes
            if (string.IsNullOrEmpty(nueva) || string.IsNullOrEmpty(confirmar))
            {
                MostrarError("Por favor llena la nueva contraseña y su confirmacion.");
                return;
            }
            if (nueva.Length < 6)
            {
                MostrarError("La nueva contraseña debe tener al menos 6 caracteres.");
                return;
            }
            if (nueva != confirmar)
            {
                MostrarError("La nueva contraseña y su confirmacion no coinciden.");
                FConfirmar.Clear();
                FConfirmar.Focus();
                return;
            }

            // Si NO es admin, hay que verificar la contrasena actual
            if (!_modoAdmin)
            {
                var actual = FActual.Password;
                if (string.IsNullOrEmpty(actual))
                {
                    MostrarError("Por favor ingresa tu contraseña actual.");
                    return;
                }

                var coincide = await App.Db.VerificarPasswordAsync(_usuario.Id, actual);
                if (!coincide)
                {
                    MostrarError("La contraseña actual es incorrecta.");
                    FActual.Clear();
                    FActual.Focus();
                    return;
                }

                if (nueva == actual)
                {
                    MostrarError("La nueva contraseña debe ser diferente a la actual.");
                    return;
                }
            }

            NuevaPassword = nueva;
            DialogResult = true;
            Close();
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void MostrarError(string mensaje)
        {
            TxtError.Text = mensaje;
            PanelError.Visibility = Visibility.Visible;
        }
    }
}
