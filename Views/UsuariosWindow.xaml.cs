using System.Windows;
using System.Windows.Controls;
using ZarelyPOS.Helpers;
using ZarelyPOS.Models;

namespace ZarelyPOS.Views
{
    public partial class UsuariosWindow : Window
    {
        private List<Usuario> _todosLosUsuarios = new();
        private Usuario? _usuarioEditando = null;
        private bool _modoEdicion = false;

        public UsuariosWindow()
        {
            InitializeComponent();
            ConfigurarHeader();
            _ = CargarUsuariosAsync();
        }

        // ─────────────────────────────────────────────
        //  INICIALIZACION
        // ─────────────────────────────────────────────

        private void ConfigurarHeader()
        {
            var u = SessionManager.UsuarioActual!;
            TxtUsuarioHeader.Text = $"{u.PrimerNombre} {u.PrimerApellido}";
        }

        private async Task CargarUsuariosAsync()
        {
            _todosLosUsuarios = await App.Db.GetUsuariosAsync();
            AplicarFiltros();
        }

        private void AplicarFiltros()
        {
            var busqueda = TxtBuscar.Text.Trim().ToLower();

            var resultado = _todosLosUsuarios.Where(u =>
                string.IsNullOrEmpty(busqueda)
                || u.PrimerNombre.ToLower().Contains(busqueda)
                || u.PrimerApellido.ToLower().Contains(busqueda)
                || u.NombreUsuario.ToLower().Contains(busqueda)
            ).ToList();

            GridUsuarios.ItemsSource = resultado;
            TxtConteo.Text = resultado.Count.ToString();
        }

        // ─────────────────────────────────────────────
        //  FORMULARIO — ABRIR / CERRAR
        // ─────────────────────────────────────────────

        private void AbrirFormularioNuevo()
        {
            _modoEdicion      = false;
            _usuarioEditando  = null;
            TxtTituloForm.Text = "Nuevo Usuario";
            BtnGuardar.Content = "Guardar";

            // Al crear: pedir contrasena. Al editar: no.
            PanelPassword.Visibility    = Visibility.Visible;
            PanelRestablecer.Visibility = Visibility.Collapsed;

            LimpiarFormulario();
            PanelFormulario.Visibility = Visibility.Visible;
        }

        private void AbrirFormularioEditar(Usuario usuario)
        {
            _modoEdicion     = true;
            _usuarioEditando = usuario;
            TxtTituloForm.Text = "Editar Usuario";
            BtnGuardar.Content = "Guardar Cambios";

            // En edicion ocultamos el campo de contrasena directa
            // y mostramos un boton para abrir el dialogo de restablecer.
            PanelPassword.Visibility    = Visibility.Collapsed;
            PanelRestablecer.Visibility = Visibility.Visible;

            FPrimerNombre.Text   = usuario.PrimerNombre;
            FPrimerApellido.Text = usuario.PrimerApellido;
            FNombreUsuario.Text  = usuario.NombreUsuario;
            FPassword.Clear();

            foreach (ComboBoxItem item in FRol.Items)
                if (item.Content?.ToString() == usuario.Rol)
                    FRol.SelectedItem = item;

            FActivo.IsChecked = usuario.Activo;

            // Proteccion: el admin no puede desactivarse a si mismo
            bool esElMismo = SessionManager.UsuarioActual?.Id == usuario.Id;
            FActivo.IsEnabled = !esElMismo;
            FRol.IsEnabled    = !esElMismo;  // tampoco puede quitarse el rol de admin

            PanelFormulario.Visibility = Visibility.Visible;
        }

        private void LimpiarFormulario()
        {
            FPrimerNombre.Text   = string.Empty;
            FPrimerApellido.Text = string.Empty;
            FNombreUsuario.Text  = string.Empty;
            FPassword.Clear();
            FRol.SelectedIndex   = 0;
            FActivo.IsChecked    = true;
            FActivo.IsEnabled    = true;
            FRol.IsEnabled       = true;
        }

        // ─────────────────────────────────────────────
        //  GUARDAR
        // ─────────────────────────────────────────────

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidarFormulario()) return;

            var nombreUsuario = FNombreUsuario.Text.Trim();
            var idAExcluir    = _modoEdicion ? _usuarioEditando!.Id : (int?)null;

            // Verificar que el nombre de usuario no este repetido
            if (await App.Db.ExisteNombreUsuarioAsync(nombreUsuario, idAExcluir))
            {
                MessageBox.Show(
                    $"El nombre de usuario '{nombreUsuario}' ya esta en uso por otro usuario.",
                    "Nombre de usuario duplicado",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_modoEdicion && _usuarioEditando != null)
            {
                // EDITAR
                _usuarioEditando.PrimerNombre   = FPrimerNombre.Text.Trim();
                _usuarioEditando.PrimerApellido = FPrimerApellido.Text.Trim();
                _usuarioEditando.NombreUsuario  = nombreUsuario;
                _usuarioEditando.Rol            = ObtenerRolSeleccionado();
                _usuarioEditando.Activo         = FActivo.IsChecked == true;

                await App.Db.UpdateUsuarioAsync(_usuarioEditando);
                MessageBox.Show("Usuario actualizado correctamente.", "Zarely POS",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // NUEVO
                var nuevo = new Usuario
                {
                    PrimerNombre   = FPrimerNombre.Text.Trim(),
                    PrimerApellido = FPrimerApellido.Text.Trim(),
                    NombreUsuario  = nombreUsuario,
                    Password       = FPassword.Password,
                    Rol            = ObtenerRolSeleccionado(),
                    Activo         = FActivo.IsChecked == true
                };

                await App.Db.AddUsuarioAsync(nuevo);
                MessageBox.Show("Usuario creado correctamente.", "Zarely POS",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            await CargarUsuariosAsync();
            PanelFormulario.Visibility = Visibility.Collapsed;
        }

        private string ObtenerRolSeleccionado()
            => (FRol.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Usuario Regular";

        private bool ValidarFormulario()
        {
            var errores = new List<string>();

            if (string.IsNullOrWhiteSpace(FPrimerNombre.Text))   errores.Add("• Primer Nombre");
            if (string.IsNullOrWhiteSpace(FPrimerApellido.Text)) errores.Add("• Primer Apellido");
            if (string.IsNullOrWhiteSpace(FNombreUsuario.Text))  errores.Add("• Nombre de Usuario");

            // La contrasena solo es obligatoria al crear
            if (!_modoEdicion)
            {
                if (string.IsNullOrEmpty(FPassword.Password))
                    errores.Add("• Contrasena");
                else if (FPassword.Password.Length < 6)
                    errores.Add("• Contrasena (minimo 6 caracteres)");
            }

            if (errores.Count > 0)
            {
                MessageBox.Show(
                    $"Por favor completa los siguientes campos:\n{string.Join("\n", errores)}",
                    "Campos requeridos",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        // ─────────────────────────────────────────────
        //  RESTABLECER CONTRASENA (lo hace el admin)
        // ─────────────────────────────────────────────

        // Boton dentro del panel de edicion
        private async void BtnRestablecer_Click(object sender, RoutedEventArgs e)
        {
            if (_usuarioEditando != null)
                await EjecutarRestablecimientoAsync(_usuarioEditando);
        }

        // Boton de icono "llave" en cada fila del DataGrid
        private async void BtnRestablecerFila_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Usuario usuario)
                await EjecutarRestablecimientoAsync(usuario);
        }

        private async Task EjecutarRestablecimientoAsync(Usuario usuario)
        {
            var dialogo = new CambiarPasswordWindow(usuario, modoAdmin: true)
            {
                Owner = this
            };

            if (dialogo.ShowDialog() == true && !string.IsNullOrEmpty(dialogo.NuevaPassword))
            {
                await App.Db.CambiarPasswordAsync(usuario.Id, dialogo.NuevaPassword);
                MessageBox.Show(
                    $"Contrasena restablecida para '{usuario.NombreCompleto}'.",
                    "Zarely POS",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ─────────────────────────────────────────────
        //  EVENTOS UI
        // ─────────────────────────────────────────────

        private async void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not Usuario usuario) return;

            // Candado 1: no permitir que alguien se borre a si mismo mientras tiene la sesion abierta.
            if (SessionManager.UsuarioActual?.Id == usuario.Id)
            {
                MessageBox.Show(
                    "No puedes eliminar el usuario con el que iniciaste sesion.\n\n" +
                    "Entra con otra cuenta de administrador para poder eliminarlo.",
                    "Eliminar usuario", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var ventas = await App.Db.ContarVentasDeUsuarioAsync(usuario.Id);
            var aviso = ventas > 0
                ? $"\n\nEste usuario registro {ventas} venta(s). Las ventas NO se eliminan: " +
                  "conservan el nombre de quien atendio."
                : string.Empty;

            var confirmar = MessageBox.Show(
                $"Eliminar permanentemente al usuario \"{usuario.NombreUsuario}\" " +
                $"({usuario.NombreCompleto})?{aviso}\n\nEsta accion no se puede deshacer.",
                "Eliminar usuario",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

            if (confirmar != MessageBoxResult.Yes) return;

            try
            {
                // Candado 2 (en el servicio): no dejar el sistema sin administrador activo.
                var (ok, mensaje) = await App.Db.DeleteUsuarioAsync(usuario.Id);

                if (!ok)
                {
                    MessageBox.Show(mensaje, "Eliminar usuario",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await CargarUsuariosAsync();
                PanelFormulario.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo eliminar el usuario:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnNuevo_Click(object sender, RoutedEventArgs e)    => AbrirFormularioNuevo();
        private void BtnCancelar_Click(object sender, RoutedEventArgs e) => PanelFormulario.Visibility = Visibility.Collapsed;
        private void BtnRegresar_Click(object sender, RoutedEventArgs e) => this.Close();
        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e) => AplicarFiltros();

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Usuario usuario)
                AbrirFormularioEditar(usuario);
        }
    }
}
