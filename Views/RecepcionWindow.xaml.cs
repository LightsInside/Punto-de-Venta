using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ZarelyPOS.Helpers;
using ZarelyPOS.Models;
using ZarelyPOS.Services;

namespace ZarelyPOS.Views
{
    /// <summary>
    /// Recepcion de mercancia: registra la entrada de producto de un proveedor.
    /// En modo "existente" suma stock a las tallas de un producto ya registrado;
    /// en modo "nuevo" da de alta el calzado con sus tallas iniciales.
    /// </summary>
    public partial class RecepcionWindow : Window
    {
        private List<Producto> _productos = new();
        private Producto? _productoSel;

        // Tallas del producto existente (talla fija, se captura la cantidad recibida).
        private readonly ObservableCollection<TallaRecepcionViewModel> _tallasExist = new();
        // Tallas de un producto nuevo (talla y cantidad editables).
        private readonly ObservableCollection<TallaRecepcionViewModel> _tallasNuevo = new();

        public RecepcionWindow()
        {
            InitializeComponent();

            TxtUsuarioHeader.Text = SessionManager.UsuarioActual is { } u
                ? $"{u.PrimerNombre} {u.PrimerApellido}"
                : string.Empty;

            ListaTallasExist.ItemsSource = _tallasExist;
            ListaTallasNuevo.ItemsSource = _tallasNuevo;

            Loaded += async (_, _) => await CargarDatosAsync();
        }

        private async Task CargarDatosAsync()
        {
            CmbProveedor.ItemsSource = await App.Db.GetProveedoresAsync();
            FCategoria.ItemsSource   = await App.Db.GetCategoriasAsync();
            _productos               = await App.Db.GetProductosAsync();

            // Arrancar el modo nuevo con una fila de talla lista para capturar.
            if (_tallasNuevo.Count == 0)
                _tallasNuevo.Add(new TallaRecepcionViewModel());

            ActualizarTallasVacias();
        }

        // ─── Alternar modo ───
        private void Modo_Changed(object sender, RoutedEventArgs e)
        {
            if (PanelExistente == null || PanelNuevo == null) return;

            var existente = RbExistente.IsChecked == true;
            PanelExistente.Visibility = existente ? Visibility.Visible : Visibility.Collapsed;
            PanelNuevo.Visibility     = existente ? Visibility.Collapsed : Visibility.Visible;

            TxtMensaje.Text = string.Empty;
            RecalcularTotal();
        }

        // ─── Buscador en vivo ───
        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e)
        {
            var q = (TxtBuscar.Text ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(q))
            {
                LstSugerencias.ItemsSource = null;
                LstSugerencias.Visibility = Visibility.Collapsed;
                return;
            }

            var filtrados = _productos.Where(p =>
                p.Nombre.ToLowerInvariant().Contains(q)
                || p.Marca.ToLowerInvariant().Contains(q)
                || p.Modelo.ToLowerInvariant().Contains(q)
                || p.Color.ToLowerInvariant().Contains(q)
            ).ToList();

            LstSugerencias.ItemsSource = filtrados
                .Select(p => new ProductoBusqueda(p, $"{p.Marca} {p.Nombre} ({p.Color})  —  stock: {p.StockTotal}"))
                .ToList();
            LstSugerencias.Visibility = filtrados.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LstSugerencias_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstSugerencias.SelectedItem is not ProductoBusqueda op) return;
            SeleccionarProducto(op.Producto);
        }

        private void SeleccionarProducto(Producto prod)
        {
            _productoSel = prod;
            TxtProductoSel.Text = $"{prod.Marca} {prod.Nombre} ({prod.Color})";

            _tallasExist.Clear();
            foreach (var t in prod.Tallas.OrderBy(t => t.Talla))
                _tallasExist.Add(new TallaRecepcionViewModel
                {
                    Talla = t.Talla,
                    StockActual = t.StockActual,
                    Cantidad = 0
                });

            LstSugerencias.Visibility = Visibility.Collapsed;
            ActualizarTallasVacias();
            RecalcularTotal();
        }

        // Agregar al producto existente una talla que aun no tiene.
        private void BtnAgregarTallaExist_Click(object sender, RoutedEventArgs e)
        {
            var talla = (TxtTallaNueva.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(talla))
            {
                Mensaje("Escribe la talla que quieres agregar.");
                return;
            }
            if (_tallasExist.Any(t => t.Talla.Equals(talla, StringComparison.OrdinalIgnoreCase)))
            {
                Mensaje($"La talla {talla} ya esta en la lista; captura la cantidad en su renglon.");
                return;
            }

            int.TryParse(TxtCantNueva.Text, out var cant);
            _tallasExist.Add(new TallaRecepcionViewModel { Talla = talla, StockActual = 0, Cantidad = cant });

            TxtTallaNueva.Text = string.Empty;
            TxtCantNueva.Text = string.Empty;
            ActualizarTallasVacias();
            RecalcularTotal();
        }

        // ─── Tallas del producto nuevo ───
        private void BtnAgregarTallaNuevo_Click(object sender, RoutedEventArgs e)
            => _tallasNuevo.Add(new TallaRecepcionViewModel());

        private void BtnQuitarTallaNuevo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is TallaRecepcionViewModel vm)
            {
                _tallasNuevo.Remove(vm);
                RecalcularTotal();
            }
        }

        private void Cantidad_Changed(object sender, TextChangedEventArgs e) => RecalcularTotal();

        private void RecalcularTotal()
        {
            var total = RbExistente.IsChecked == true
                ? _tallasExist.Sum(t => t.Cantidad)
                : _tallasNuevo.Sum(t => t.Cantidad);
            if (TxtTotalRecibir != null)
                TxtTotalRecibir.Text = total.ToString();
        }

        private void ActualizarTallasVacias()
        {
            if (TxtSinTallas != null)
                TxtSinTallas.Visibility = _tallasExist.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ─── Registrar ───
        private async void BtnRegistrar_Click(object sender, RoutedEventArgs e)
        {
            TxtMensaje.Text = string.Empty;

            if (CmbProveedor.SelectedItem is not Proveedor proveedor)
            {
                Mensaje("Selecciona el proveedor de la mercancia.");
                return;
            }

            try
            {
                BtnRegistrar.IsEnabled = false;

                if (RbExistente.IsChecked == true)
                    await RegistrarExistenteAsync(proveedor);
                else
                    await RegistrarNuevoAsync(proveedor);
            }
            catch (Exception ex)
            {
                Mensaje($"No se pudo registrar la recepcion: {ex.Message}");
            }
            finally
            {
                BtnRegistrar.IsEnabled = true;
            }
        }

        private async Task RegistrarExistenteAsync(Proveedor proveedor)
        {
            if (_productoSel == null)
            {
                Mensaje("Busca y selecciona el producto que estas recibiendo.");
                return;
            }

            var entradas = _tallasExist
                .Where(t => t.Cantidad > 0 && !string.IsNullOrWhiteSpace(t.Talla))
                .Select(t => (t.Talla.Trim(), t.Cantidad))
                .ToList();

            if (entradas.Count == 0)
            {
                Mensaje("Captura la cantidad recibida en al menos una talla.");
                return;
            }

            var ok = await App.Db.RecepcionarInventarioAsync(_productoSel.Id, proveedor.Id, entradas);
            if (!ok)
            {
                Mensaje("El producto ya no existe. Actualiza la busqueda.");
                return;
            }

            var pares = entradas.Sum(x => x.Cantidad);
            MessageBox.Show(
                $"Recepcion registrada.\n\nSe agregaron {pares} par(es) al inventario de " +
                $"\"{_productoSel.Marca} {_productoSel.Nombre}\".",
                "Recepcion de mercancia", MessageBoxButton.OK, MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }

        private async Task RegistrarNuevoAsync(Proveedor proveedor)
        {
            var errores = new List<string>();

            var marca  = (FMarca.Text  ?? string.Empty).Trim();
            var nombre = (FNombre.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(marca))  errores.Add("La marca es obligatoria.");
            if (string.IsNullOrWhiteSpace(nombre)) errores.Add("El nombre o modelo es obligatorio.");

            if (!decimal.TryParse(FPrecio.Text, out var precio) || precio <= 0)
                errores.Add("Captura un precio de venta valido.");

            decimal? costo = null;
            if (!string.IsNullOrWhiteSpace(FCosto.Text))
            {
                if (decimal.TryParse(FCosto.Text, out var c)) costo = c;
                else errores.Add("El costo unitario no es un numero valido.");
            }

            int? reorden = null;
            if (!string.IsNullOrWhiteSpace(FReorden.Text))
            {
                if (int.TryParse(FReorden.Text, out var r)) reorden = r;
                else errores.Add("El punto de reorden no es un numero valido.");
            }

            // Tallas validas (talla no vacia y cantidad > 0), sin duplicados.
            var tallas = _tallasNuevo
                .Where(t => !string.IsNullOrWhiteSpace(t.Talla) && t.Cantidad > 0)
                .ToList();

            if (tallas.Count == 0)
                errores.Add("Agrega al menos una talla con cantidad recibida.");

            var duplicados = tallas
                .GroupBy(t => t.Talla.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicados.Count > 0)
                errores.Add($"Tallas duplicadas: {string.Join(", ", duplicados)}");

            if (errores.Count > 0)
            {
                Mensaje(string.Join("  ·  ", errores));
                return;
            }

            var producto = new Producto
            {
                Marca         = marca,
                Nombre        = nombre,
                Modelo        = (FModelo.Text ?? string.Empty).Trim(),
                Color         = (FColor.Text  ?? string.Empty).Trim(),
                Precio        = precio,
                CostoUnitario = costo,
                PuntoReorden  = reorden,
                Estado        = "Activo",
                CategoriaId   = (FCategoria.SelectedItem as Categoria)?.Id,
                ProveedorId   = proveedor.Id,
                Tallas = tallas.Select(t => new TallaInventario
                {
                    Talla = t.Talla.Trim(),
                    StockActual = t.Cantidad
                }).ToList()
            };

            await App.Db.AddProductoAsync(producto);

            var pares = tallas.Sum(t => t.Cantidad);
            MessageBox.Show(
                $"Producto dado de alta.\n\n\"{marca} {nombre}\" se registro con {pares} par(es) " +
                "en inventario.",
                "Recepcion de mercancia", MessageBoxButton.OK, MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }

        private void Mensaje(string texto) => TxtMensaje.Text = texto;

        private void BtnRegresar_Click(object sender, RoutedEventArgs e) => Close();
    }

    /// <summary>Opcion del buscador de productos (producto + texto a mostrar).</summary>
    public class ProductoBusqueda
    {
        public Producto Producto { get; }
        public string Display { get; }
        public ProductoBusqueda(Producto producto, string display)
        {
            Producto = producto;
            Display = display;
        }
    }

    /// <summary>Renglon de talla en recepcion: la talla y la cantidad que se esta recibiendo.</summary>
    public class TallaRecepcionViewModel : INotifyPropertyChanged
    {
        private string _talla = string.Empty;
        private int _cantidad;

        public string Talla
        {
            get => _talla;
            set { _talla = value ?? string.Empty; OnPropertyChanged(nameof(Talla)); }
        }

        public int StockActual { get; set; }

        public int Cantidad
        {
            get => _cantidad;
            set { _cantidad = value; OnPropertyChanged(nameof(Cantidad)); }
        }

        public string StockTexto => $"Stock actual: {StockActual}";

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string p)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
