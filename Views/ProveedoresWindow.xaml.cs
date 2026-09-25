using System.Windows;
using System.Windows.Controls;
using ZarelyPOS.Helpers;
using ZarelyPOS.Models;

namespace ZarelyPOS.Views
{
    public partial class ProveedoresWindow : Window
    {
        private List<Proveedor> _todosLosProveedores = new();
        private Proveedor? _proveedorEditando = null;
        private bool _modoEdicion = false;

        public ProveedoresWindow()
        {
            InitializeComponent();
            ConfigurarHeader();
            this.Loaded += ProveedoresWindow_Loaded;
        }

        private async void ProveedoresWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            this.Loaded -= ProveedoresWindow_Loaded;
            await CargarProveedoresAsync();
        }

        // ─── Inicializacion ──────────────────────────────────────

        private void ConfigurarHeader()
        {
            var u = SessionManager.UsuarioActual!;
            TxtUsuarioHeader.Text = $"{u.PrimerNombre} {u.PrimerApellido}";
        }

        private async Task CargarProveedoresAsync()
        {
            _todosLosProveedores = await App.Db.GetProveedoresAsync();
            AplicarFiltros();
        }

        // ─── Filtros ─────────────────────────────────────────────

        private void Filtros_Changed(object sender, System.EventArgs e) => AplicarFiltros();

        private void AplicarFiltros()
        {
            if (GridProveedores == null || TxtConteo == null) return;

            var busqueda = (TxtBuscar.Text ?? string.Empty).Trim().ToLowerInvariant();

            var resultado = _todosLosProveedores.Where(p =>
                string.IsNullOrEmpty(busqueda)
                || p.Nombre.ToLowerInvariant().Contains(busqueda)
                || p.Telefono.ToLowerInvariant().Contains(busqueda)
                || p.Correo.ToLowerInvariant().Contains(busqueda)
                || p.Direccion.ToLowerInvariant().Contains(busqueda)
            ).ToList();

            GridProveedores.ItemsSource = resultado;
            TxtConteo.Text = resultado.Count.ToString();
        }

        // ─── Formulario ──────────────────────────────────────────

        private void AbrirFormularioNuevo()
        {
            _modoEdicion       = false;
            _proveedorEditando = null;
            TxtTituloForm.Text = "Nuevo Proveedor";
            BtnGuardar.Content = "Guardar";

            LimpiarFormulario();
            PanelFormulario.Visibility = Visibility.Visible;
            FNombre.Focus();
        }

        private void AbrirFormularioEditar(Proveedor proveedor)
        {
            _modoEdicion       = true;
            _proveedorEditando = proveedor;
            TxtTituloForm.Text = "Editar Proveedor";
            BtnGuardar.Content = "Guardar Cambios";

            FNombre.Text    = proveedor.Nombre;
            FTelefono.Text  = proveedor.Telefono;
            FCorreo.Text    = proveedor.Correo;
            FDireccion.Text = proveedor.Direccion;

            PanelFormulario.Visibility = Visibility.Visible;
            FNombre.Focus();
        }

        private void LimpiarFormulario()
        {
            FNombre.Text    = string.Empty;
            FTelefono.Text  = string.Empty;
            FCorreo.Text    = string.Empty;
            FDireccion.Text = string.Empty;
        }

        // ─── Guardar ─────────────────────────────────────────────

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidarFormulario()) return;

            var nombre     = FNombre.Text.Trim();
            var idAExcluir = _modoEdicion ? _proveedorEditando!.Id : (int?)null;

            // Verificar nombre unico
            if (await App.Db.ExisteNombreProveedorAsync(nombre, idAExcluir))
            {
                MessageBox.Show(
                    $"Ya existe un proveedor registrado con el nombre '{nombre}'.",
                    "Nombre duplicado",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (_modoEdicion && _proveedorEditando != null)
                {
                    _proveedorEditando.Nombre    = nombre;
                    _proveedorEditando.Telefono  = FTelefono.Text.Trim();
                    _proveedorEditando.Correo    = FCorreo.Text.Trim();
                    _proveedorEditando.Direccion = FDireccion.Text.Trim();

                    await App.Db.UpdateProveedorAsync(_proveedorEditando);

                    MessageBox.Show("Proveedor actualizado correctamente.",
                        "Zarely POS", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var nuevo = new Proveedor
                    {
                        Nombre    = nombre,
                        Telefono  = FTelefono.Text.Trim(),
                        Correo    = FCorreo.Text.Trim(),
                        Direccion = FDireccion.Text.Trim()
                    };
                    await App.Db.AddProveedorAsync(nuevo);

                    MessageBox.Show("Proveedor creado correctamente.",
                        "Zarely POS", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                await CargarProveedoresAsync();
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

            if (string.IsNullOrWhiteSpace(FNombre.Text))
                errores.Add("• Nombre / Razon social");

            // Correo: si se captura, validar formato basico
            var correo = FCorreo.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(correo) && !correo.Contains("@"))
                errores.Add("• Correo electronico (formato no valido)");

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

        // ─── Eliminar ────────────────────────────────────────────

        private async void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not Proveedor proveedor) return;

            // Avisar si hay productos asociados (quedarian sin proveedor)
            var productosAsociados = await App.Db.ContarProductosDeProveedorAsync(proveedor.Id);

            var mensaje = $"¿Eliminar al proveedor '{proveedor.Nombre}'?";
            if (productosAsociados > 0)
                mensaje += $"\n\nAtencion: hay {productosAsociados} producto(s) asignado(s) a este " +
                           "proveedor. No se eliminaran, pero quedaran SIN proveedor asignado.";

            var confirmacion = MessageBox.Show(
                mensaje, "Confirmar eliminacion",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirmacion != MessageBoxResult.Yes) return;

            try
            {
                await App.Db.DeleteProveedorAsync(proveedor.Id);
                MessageBox.Show($"Proveedor '{proveedor.Nombre}' eliminado.",
                    "Zarely POS", MessageBoxButton.OK, MessageBoxImage.Information);
                await CargarProveedoresAsync();

                // Si estabamos editando justo a ese proveedor, cerrar el formulario
                if (_modoEdicion && _proveedorEditando?.Id == proveedor.Id)
                    PanelFormulario.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar: {ex.Message}",
                    "Zarely POS", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Eventos de botones ──────────────────────────────────

        private void BtnNuevo_Click(object sender, RoutedEventArgs e)    => AbrirFormularioNuevo();
        private void BtnCancelar_Click(object sender, RoutedEventArgs e) => PanelFormulario.Visibility = Visibility.Collapsed;
        private void BtnRegresar_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Proveedor proveedor)
                AbrirFormularioEditar(proveedor);
        }
    }
}
