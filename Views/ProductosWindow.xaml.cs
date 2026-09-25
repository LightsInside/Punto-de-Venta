using System.Windows;
using System.Windows.Controls;
using ZarelyPOS.Helpers;
using ZarelyPOS.Models;

namespace ZarelyPOS.Views
{
    public partial class ProductosWindow : Window
    {
        private List<Producto> _todosLosProductos = new();
        private List<Categoria> _categorias = new();
        private List<Proveedor> _proveedores = new();

        // Centinelas para los combos "Todos"
        private static readonly Categoria  TodasCategorias  = new() { Id = -1, Nombre = "Todas" };
        private static readonly Proveedor  TodosProveedores = new() { Id = -1, Nombre = "Todos" };

        public ProductosWindow()
        {
            InitializeComponent();
            ConfigurarHeader();
            ConfigurarPermisos();
            _ = CargarDatosAsync();
        }

        // ─── Inicializacion ──────────────────────────────────────

        private void ConfigurarHeader()
        {
            var u = SessionManager.UsuarioActual!;
            TxtUsuarioHeader.Text = $"{u.PrimerNombre} {u.PrimerApellido}";
        }

        private void ConfigurarPermisos()
        {
            // Si NO es admin, ocultar el boton de "Nuevo", la columna de acciones,
            // y mostrar el banner de solo lectura.
            if (!SessionManager.EsAdmin)
            {
                BtnNuevo.Visibility       = Visibility.Collapsed;
                ColAcciones.Visibility    = Visibility.Collapsed;
                BannerSoloLectura.Visibility = Visibility.Visible;
            }
        }

        private async Task CargarDatosAsync()
        {
            // Cargar catalogos para los combos de filtros
            _categorias  = await App.Db.GetCategoriasAsync();
            _proveedores = await App.Db.GetProveedoresAsync();

            // Combos de filtros: incluir "Todos" al inicio
            var listaCat = new List<Categoria> { TodasCategorias };
            listaCat.AddRange(_categorias);
            CmbFiltroCategoria.ItemsSource       = listaCat;
            CmbFiltroCategoria.DisplayMemberPath = nameof(Categoria.Nombre);
            CmbFiltroCategoria.SelectedIndex     = 0;

            var listaProv = new List<Proveedor> { TodosProveedores };
            listaProv.AddRange(_proveedores);
            CmbFiltroProveedor.ItemsSource       = listaProv;
            CmbFiltroProveedor.DisplayMemberPath = nameof(Proveedor.Nombre);
            CmbFiltroProveedor.SelectedIndex     = 0;

            await RecargarProductosAsync();
        }

        private async Task RecargarProductosAsync()
        {
            _todosLosProductos = await App.Db.GetProductosAsync();
            AplicarFiltros();
        }

        // ─── Filtros ──────────────────────────────────────────────

        private void Filtros_Changed(object sender, System.EventArgs e) => AplicarFiltros();

        private void AplicarFiltros()
        {
            var busqueda = (TxtBuscar.Text ?? string.Empty).Trim().ToLowerInvariant();
            var catId    = (CmbFiltroCategoria.SelectedItem as Categoria)?.Id ?? -1;
            var provId   = (CmbFiltroProveedor.SelectedItem as Proveedor)?.Id ?? -1;

            var resultado = _todosLosProductos.Where(p =>
                    (string.IsNullOrEmpty(busqueda)
                     || p.Nombre.ToLowerInvariant().Contains(busqueda)
                     || p.Marca.ToLowerInvariant().Contains(busqueda)
                     || p.Modelo.ToLowerInvariant().Contains(busqueda)
                     || p.Color.ToLowerInvariant().Contains(busqueda))
                 && (catId  == -1 || p.CategoriaId == catId)
                 && (provId == -1 || p.ProveedorId == provId)
                ).ToList();

            GridProductos.ItemsSource = resultado;
            TxtConteo.Text = resultado.Count.ToString();
        }

        // ─── Acciones (admin) ────────────────────────────────────

        private async void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            var formulario = new ProductoFormWindow(producto: null)
            {
                Owner = this
            };
            if (formulario.ShowDialog() == true)
            {
                await RecargarProductosAsync();
            }
        }

        private async void BtnEditar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not Producto producto) return;

            // Recargamos el producto desde la BD para tener sus tallas frescas
            var completo = await App.Db.GetProductoByIdAsync(producto.Id);
            if (completo == null)
            {
                MessageBox.Show("El producto ya no existe en la base de datos.",
                    "Zarely POS", MessageBoxButton.OK, MessageBoxImage.Warning);
                await RecargarProductosAsync();
                return;
            }

            var formulario = new ProductoFormWindow(producto: completo) { Owner = this };
            if (formulario.ShowDialog() == true)
            {
                await RecargarProductosAsync();
            }
        }

        private async void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not Producto producto) return;

            var confirmacion = MessageBox.Show(
                $"¿Estas seguro de eliminar el producto '{producto.Nombre}'?\n" +
                "Tambien se eliminaran todas sus tallas y stock asociado.",
                "Confirmar eliminacion",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirmacion != MessageBoxResult.Yes) return;

            await App.Db.DeleteProductoAsync(producto.Id);

            // Borrar tambien la imagen asociada si tenia
            ImagenHelper.BorrarImagen(producto.RutaImagen);

            MessageBox.Show($"Producto '{producto.Nombre}' eliminado.",
                "Zarely POS", MessageBoxButton.OK, MessageBoxImage.Information);

            await RecargarProductosAsync();
        }

        // ─── Cierre ──────────────────────────────────────────────

        private void BtnRegresar_Click(object sender, RoutedEventArgs e) => Close();
    }
}
