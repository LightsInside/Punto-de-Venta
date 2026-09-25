using System.Windows;
using System.Windows.Controls;
using ZarelyPOS.Helpers;
using ZarelyPOS.Models;

namespace ZarelyPOS.Views
{
    public partial class ClientesWindow : Window
    {
        private List<Cliente> _todosLosClientes = new();
        private Cliente? _clienteEditando = null;
        private bool _modoEdicion = false;

        public ClientesWindow()
        {
            InitializeComponent();
            ConfigurarHeader();
            ConfigurarPermisos();
            this.Loaded += ClientesWindow_Loaded;
        }

        private async void ClientesWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            this.Loaded -= ClientesWindow_Loaded;
            await CargarClientesAsync();
        }

        // ─── Inicializacion ──────────────────────────────────────

        private void ConfigurarHeader()
        {
            var u = SessionManager.UsuarioActual!;
            TxtUsuarioHeader.Text = $"{u.PrimerNombre} {u.PrimerApellido}";
        }

        /// <summary>
        /// Permisos segun el rol:
        ///   - Admin: ve todo, puede crear, editar y activar/desactivar.
        ///   - Regular: puede ver la lista y crear clientes nuevos, pero NO editar
        ///     ni activar/desactivar (solo el admin puede modificar registros existentes).
        /// </summary>
        private void ConfigurarPermisos()
        {
            if (!SessionManager.EsAdmin)
            {
                ColAcciones.Visibility       = Visibility.Collapsed;
                BannerSoloAgregar.Visibility = Visibility.Visible;
                // El boton "+ Nuevo Cliente" si lo dejamos visible: usuarios regulares
                // si pueden agregar (lo definimos asi en las preguntas iniciales).
            }
        }

        private async Task CargarClientesAsync()
        {
            _todosLosClientes = await App.Db.GetClientesAsync();
            AplicarFiltros();
        }

        // ─── Filtros ─────────────────────────────────────────────

        private void Filtros_Changed(object sender, System.EventArgs e) => AplicarFiltros();

        private void AplicarFiltros()
        {
            if (GridClientes == null || TxtConteo == null) return;

            var busqueda = (TxtBuscar.Text ?? string.Empty).Trim().ToLowerInvariant();
            var filtroEstado = (CmbFiltroEstado.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Todos";

            var resultado = _todosLosClientes.Where(c =>
                (string.IsNullOrEmpty(busqueda)
                 || c.Nombre.ToLowerInvariant().Contains(busqueda)
                 || c.Apellido.ToLowerInvariant().Contains(busqueda)
                 || c.Telefono.ToLowerInvariant().Contains(busqueda)
                 || c.Correo.ToLowerInvariant().Contains(busqueda)
                 || (c.RFC?.ToLowerInvariant().Contains(busqueda) ?? false))
             && (filtroEstado == "Todos"
                 || (filtroEstado == "Activos"   && c.Activo)
                 || (filtroEstado == "Inactivos" && !c.Activo))
            ).ToList();

            GridClientes.ItemsSource = resultado;
            TxtConteo.Text = resultado.Count.ToString();
        }

        // ─── Formulario ──────────────────────────────────────────

        private void AbrirFormularioNuevo()
        {
            _modoEdicion     = false;
            _clienteEditando = null;
            TxtTituloForm.Text = "Nuevo Cliente";
            BtnGuardar.Content = "Guardar";

            LimpiarFormulario();

            // En modo nuevo, el checkbox "Activo" no es relevante para el usuario:
            // los clientes nuevos siempre se crean activos. Lo ocultamos.
            FActivo.Visibility = Visibility.Collapsed;
            FActivo.IsChecked  = true;

            PanelFormulario.Visibility = Visibility.Visible;
        }

        private void AbrirFormularioEditar(Cliente cliente)
        {
            _modoEdicion     = true;
            _clienteEditando = cliente;
            TxtTituloForm.Text = "Editar Cliente";
            BtnGuardar.Content = "Guardar Cambios";

            FNombre.Text       = cliente.Nombre;
            FApellido.Text     = cliente.Apellido;
            FTelefono.Text     = cliente.Telefono;
            FCorreo.Text       = cliente.Correo;
            FRFC.Text          = cliente.RFC ?? string.Empty;
            FDireccion.Text    = cliente.Direccion;
            FFechaNacimiento.SelectedDate = cliente.FechaNacimiento;
            FNotas.Text        = cliente.Notas;
            FActivo.IsChecked  = cliente.Activo;

            // En modo editar si mostramos el checkbox
            FActivo.Visibility = Visibility.Visible;

            PanelFormulario.Visibility = Visibility.Visible;
        }

        private void LimpiarFormulario()
        {
            FNombre.Text       = string.Empty;
            FApellido.Text     = string.Empty;
            FTelefono.Text     = string.Empty;
            FCorreo.Text       = string.Empty;
            FRFC.Text          = string.Empty;
            FDireccion.Text    = string.Empty;
            FFechaNacimiento.SelectedDate = null;
            FNotas.Text        = string.Empty;
            FActivo.IsChecked  = true;
        }

        // ─── Guardar ─────────────────────────────────────────────

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidarFormulario()) return;

            var rfc = FRFC.Text?.Trim() ?? string.Empty;
            var idAExcluir = _modoEdicion ? _clienteEditando!.Id : (int?)null;

            // Verificar RFC unico (solo si se capturo uno)
            if (!string.IsNullOrEmpty(rfc) && await App.Db.ExisteRFCAsync(rfc, idAExcluir))
            {
                MessageBox.Show(
                    $"El RFC '{rfc.ToUpperInvariant()}' ya esta registrado para otro cliente.",
                    "RFC duplicado",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (_modoEdicion && _clienteEditando != null)
                {
                    _clienteEditando.Nombre          = FNombre.Text.Trim();
                    _clienteEditando.Apellido        = FApellido.Text.Trim();
                    _clienteEditando.Telefono        = FTelefono.Text.Trim();
                    _clienteEditando.Correo          = FCorreo.Text.Trim();
                    _clienteEditando.RFC             = string.IsNullOrWhiteSpace(rfc) ? null : rfc;
                    _clienteEditando.Direccion       = FDireccion.Text.Trim();
                    _clienteEditando.FechaNacimiento = FFechaNacimiento.SelectedDate;
                    _clienteEditando.Notas           = FNotas.Text.Trim();
                    _clienteEditando.Activo          = FActivo.IsChecked == true;

                    await App.Db.UpdateClienteAsync(_clienteEditando);

                    MessageBox.Show("Cliente actualizado correctamente.",
                        "Zarely POS", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var nuevo = new Cliente
                    {
                        Nombre          = FNombre.Text.Trim(),
                        Apellido        = FApellido.Text.Trim(),
                        Telefono        = FTelefono.Text.Trim(),
                        Correo          = FCorreo.Text.Trim(),
                        RFC             = string.IsNullOrWhiteSpace(rfc) ? null : rfc,
                        Direccion       = FDireccion.Text.Trim(),
                        FechaNacimiento = FFechaNacimiento.SelectedDate,
                        Notas           = FNotas.Text.Trim(),
                        Activo          = true,
                        FechaRegistro   = DateTime.Now
                    };
                    await App.Db.AddClienteAsync(nuevo);

                    MessageBox.Show("Cliente creado correctamente.",
                        "Zarely POS", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                await CargarClientesAsync();
                PanelFormulario.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}",
                    "Zarely POS", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidarFormulario()
        {
            var errores = new List<string>();

            if (string.IsNullOrWhiteSpace(FNombre.Text))   errores.Add("• Nombre");
            if (string.IsNullOrWhiteSpace(FApellido.Text)) errores.Add("• Apellido");

            // Correo: si se captura, validar formato basico
            var correo = FCorreo.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(correo) && !correo.Contains("@"))
                errores.Add("• Correo electronico (formato no valido)");

            // RFC: si se captura, validar longitud (12 o 13 caracteres)
            var rfc = FRFC.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(rfc) && rfc.Length < 12)
                errores.Add("• RFC (debe tener 12 o 13 caracteres, o dejarlo vacio)");

            if (errores.Count > 0)
            {
                MessageBox.Show(
                    $"Por favor corrige lo siguiente:\n\n{string.Join("\n", errores)}",
                    "Campos requeridos",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        // ─── Activar / Desactivar (solo admin) ───────────────────

        private async void BtnAlternarEstado_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not Cliente cliente) return;

            var accion = cliente.Activo ? "desactivar" : "reactivar";
            var confirmacion = MessageBox.Show(
                $"¿Estas seguro de {accion} al cliente '{cliente.NombreCompleto}'?\n\n" +
                (cliente.Activo
                    ? "El cliente no aparecera en busquedas de ventas nuevas, pero su historial se conserva."
                    : "El cliente volvera a aparecer en busquedas de ventas."),
                $"Confirmar {accion}",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirmacion != MessageBoxResult.Yes) return;

            cliente.Activo = !cliente.Activo;
            await App.Db.UpdateClienteAsync(cliente);

            MessageBox.Show(
                $"Cliente '{cliente.NombreCompleto}' {(cliente.Activo ? "reactivado" : "desactivado")}.",
                "Zarely POS", MessageBoxButton.OK, MessageBoxImage.Information);

            await CargarClientesAsync();
        }

        // ─── Eventos de botones ──────────────────────────────────

        private async void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not Cliente cliente) return;

            // Avisar si el cliente tiene historial: las ventas se conservan, pero conviene saberlo.
            var ventas = await App.Db.ContarVentasDeClienteAsync(cliente.Id);
            var aviso = ventas > 0
                ? $"\n\nEste cliente tiene {ventas} venta(s) registrada(s). Las ventas NO se eliminan: " +
                  "conservan el nombre del cliente y solo pierden el vinculo."
                : string.Empty;

            var confirmar = MessageBox.Show(
                $"Eliminar permanentemente a \"{cliente.Nombre} {cliente.Apellido}\"?{aviso}\n\n" +
                "Esta accion no se puede deshacer.",
                "Eliminar cliente",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

            if (confirmar != MessageBoxResult.Yes) return;

            try
            {
                if (await App.Db.DeleteClienteAsync(cliente.Id))
                {
                    await CargarClientesAsync();
                    PanelFormulario.Visibility = Visibility.Collapsed;
                }
                else
                {
                    MessageBox.Show("El cliente ya no existe.", "Eliminar cliente",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    await CargarClientesAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo eliminar el cliente:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnNuevo_Click(object sender, RoutedEventArgs e)    => AbrirFormularioNuevo();
        private void BtnCancelar_Click(object sender, RoutedEventArgs e) => PanelFormulario.Visibility = Visibility.Collapsed;
        private void BtnRegresar_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Cliente cliente)
                AbrirFormularioEditar(cliente);
        }
    }
}
