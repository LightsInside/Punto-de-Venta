using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ZarelyPOS.Helpers;
using ZarelyPOS.Models;

namespace ZarelyPOS.Views
{
    public partial class ProductoFormWindow : Window
    {
        // null en modo "nuevo", instancia en modo "editar"
        private readonly Producto? _productoOriginal;
        private readonly bool _modoEdicion;

        private List<Categoria> _categorias = new();
        private List<Proveedor> _proveedores = new();

        // Ruta de la imagen que se va a guardar al hacer clic en "Guardar".
        // null = no hay imagen. Si es ruta absoluta, es una imagen nueva por copiar.
        // Si es ruta relativa, es la imagen actual del producto (modo edicion).
        private string? _rutaImagenPendiente;

        // Tallas en edicion. Usamos ObservableCollection para que el ItemsControl
        // se actualice automaticamente al agregar/quitar.
        private readonly ObservableCollection<TallaEditableViewModel> _tallas = new();

        public ProductoFormWindow(Producto? producto)
        {
            InitializeComponent();
            _productoOriginal = producto;
            _modoEdicion      = producto != null;

            TxtTitulo.Text = _modoEdicion ? "Editar Producto" : "Nuevo Producto";

            ListaTallas.ItemsSource = _tallas;
            _tallas.CollectionChanged += (_, _) => RecalcularTotal();

            _ = InicializarAsync();
        }

        // ─── Carga inicial ──────────────────────────────────────

        private async Task InicializarAsync()
        {
            _categorias  = await App.Db.GetCategoriasAsync();
            _proveedores = await App.Db.GetProveedoresAsync();

            // Llenar combos (con opcion "(sin asignar)" al inicio)
            var listaCat = new List<Categoria> { new() { Id = 0, Nombre = "(sin categoria)" } };
            listaCat.AddRange(_categorias);
            FCategoria.ItemsSource       = listaCat;
            FCategoria.DisplayMemberPath = nameof(Categoria.Nombre);
            FCategoria.SelectedIndex     = 0;

            var listaProv = new List<Proveedor> { new() { Id = 0, Nombre = "(sin proveedor)" } };
            listaProv.AddRange(_proveedores);
            FProveedor.ItemsSource       = listaProv;
            FProveedor.DisplayMemberPath = nameof(Proveedor.Nombre);
            FProveedor.SelectedIndex     = 0;

            if (_modoEdicion && _productoOriginal != null)
            {
                CargarDatosParaEdicion(_productoOriginal);
            }
            else
            {
                // En modo "nuevo", arrancar con una talla vacia para que el usuario vea como funciona
                _tallas.Add(new TallaEditableViewModel("", 0));
            }

            RecalcularTotal();
        }

        private void CargarDatosParaEdicion(Producto p)
        {
            FNombre.Text        = p.Nombre;
            FMarca.Text         = p.Marca;
            FModelo.Text        = p.Modelo;
            FColor.Text         = p.Color;
            FPrecio.Text        = p.Precio.ToString("0.##", CultureInfo.InvariantCulture);
            FCostoUnitario.Text = p.CostoUnitario?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;
            FPuntoReorden.Text  = p.PuntoReorden?.ToString() ?? string.Empty;

            // Estado
            foreach (ComboBoxItem item in FEstado.Items)
                if (item.Content?.ToString() == p.Estado)
                    FEstado.SelectedItem = item;

            // Categoria
            if (p.CategoriaId.HasValue)
            {
                var cat = (FCategoria.ItemsSource as IEnumerable<Categoria>)?.FirstOrDefault(c => c.Id == p.CategoriaId);
                if (cat != null) FCategoria.SelectedItem = cat;
            }

            // Proveedor
            if (p.ProveedorId.HasValue)
            {
                var prov = (FProveedor.ItemsSource as IEnumerable<Proveedor>)?.FirstOrDefault(x => x.Id == p.ProveedorId);
                if (prov != null) FProveedor.SelectedItem = prov;
            }

            // Imagen
            if (!string.IsNullOrEmpty(p.RutaImagen))
            {
                _rutaImagenPendiente = p.RutaImagen;
                MostrarPreviewImagen(p.RutaImagen);
            }

            // Tallas
            _tallas.Clear();
            foreach (var t in p.Tallas.OrderBy(t => t.Talla))
                _tallas.Add(new TallaEditableViewModel(t.Talla, t.StockActual));
        }

        // ─── Imagen ──────────────────────────────────────────────

        private void BtnSeleccionarImagen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Seleccionar imagen del producto",
                Filter = "Imagenes (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|Todos (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            // Guardar la ruta absoluta - se copiara a /Images al hacer "Guardar"
            _rutaImagenPendiente = dlg.FileName;
            MostrarPreviewImagen(dlg.FileName);
        }

        private void BtnQuitarImagen_Click(object sender, RoutedEventArgs e)
        {
            _rutaImagenPendiente = null;
            ImgPreview.Source = null;
            PanelSinImagen.Visibility = Visibility.Visible;
            BtnQuitarImagen.IsEnabled = false;
        }

        private void MostrarPreviewImagen(string ruta)
        {
            // ImagenHelper.CargarBitmap acepta tanto rutas relativas como absolutas
            var bmp = ImagenHelper.CargarBitmap(ruta);
            ImgPreview.Source = bmp;
            PanelSinImagen.Visibility = bmp == null ? Visibility.Visible : Visibility.Collapsed;
            BtnQuitarImagen.IsEnabled = bmp != null;
        }

        // ─── Tallas ──────────────────────────────────────────────

        private void BtnAgregarTalla_Click(object sender, RoutedEventArgs e)
            => _tallas.Add(new TallaEditableViewModel("", 0));

        private void BtnQuitarTalla_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TallaEditableViewModel t)
            {
                _tallas.Remove(t);
            }
        }

        private void StockTalla_Changed(object sender, TextChangedEventArgs e)
            => RecalcularTotal();

        private void RecalcularTotal()
        {
            int total = _tallas.Sum(t => t.StockActual);
            TxtStockTotal.Text = total.ToString();
        }

        // ─── Guardar ─────────────────────────────────────────────

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidarFormulario(out var producto, out var tallasValidas)) return;

            // Si hay una imagen nueva por copiar (ruta absoluta), copiarla a /Images
            // y reemplazar _rutaImagenPendiente por la ruta relativa.
            string? rutaImagenFinal = _rutaImagenPendiente;
            if (!string.IsNullOrEmpty(rutaImagenFinal) && Path.IsPathRooted(rutaImagenFinal))
            {
                try
                {
                    var rutaRelativa = ImagenHelper.GuardarImagen(rutaImagenFinal);

                    // Si estabamos editando y habia una imagen vieja distinta, borrarla
                    if (_modoEdicion && _productoOriginal != null
                        && !string.IsNullOrEmpty(_productoOriginal.RutaImagen)
                        && _productoOriginal.RutaImagen != rutaRelativa)
                    {
                        ImagenHelper.BorrarImagen(_productoOriginal.RutaImagen);
                    }

                    rutaImagenFinal = rutaRelativa;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"No se pudo guardar la imagen: {ex.Message}",
                        "Zarely POS", MessageBoxButton.OK, MessageBoxImage.Warning);
                    rutaImagenFinal = null;
                }
            }
            else if (string.IsNullOrEmpty(rutaImagenFinal) && _modoEdicion && _productoOriginal != null
                     && !string.IsNullOrEmpty(_productoOriginal.RutaImagen))
            {
                // Se quito la imagen: borrar el archivo viejo
                ImagenHelper.BorrarImagen(_productoOriginal.RutaImagen);
                rutaImagenFinal = null;
            }

            producto.RutaImagen = rutaImagenFinal;

            try
            {
                if (_modoEdicion && _productoOriginal != null)
                {
                    producto.Id = _productoOriginal.Id;
                    var tallasModel = tallasValidas.Select(t => new TallaInventario
                    {
                        Talla = t.Talla.Trim(),
                        StockActual = t.StockActual
                    });
                    await App.Db.UpdateProductoAsync(producto, tallasModel);
                }
                else
                {
                    producto.Tallas = tallasValidas.Select(t => new TallaInventario
                    {
                        Talla = t.Talla.Trim(),
                        StockActual = t.StockActual
                    }).ToList();
                    await App.Db.AddProductoAsync(producto);
                }

                MessageBox.Show(
                    _modoEdicion ? "Producto actualizado correctamente." : "Producto creado correctamente.",
                    "Zarely POS", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}",
                    "Zarely POS", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ─── Validacion ──────────────────────────────────────────

        private bool ValidarFormulario(out Producto producto, out List<TallaEditableViewModel> tallasValidas)
        {
            producto      = new Producto();
            tallasValidas = new List<TallaEditableViewModel>();

            var errores = new List<string>();

            if (string.IsNullOrWhiteSpace(FNombre.Text))
                errores.Add("• Nombre del producto");

            if (!decimal.TryParse(FPrecio.Text?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var precio)
                || precio < 0)
                errores.Add("• Precio venta (numero valido, mayor o igual a 0)");

            decimal? costo = null;
            if (!string.IsNullOrWhiteSpace(FCostoUnitario.Text))
            {
                if (decimal.TryParse(FCostoUnitario.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var c) && c >= 0)
                    costo = c;
                else
                    errores.Add("• Costo unitario (debe ser un numero valido)");
            }

            int? puntoReorden = null;
            if (!string.IsNullOrWhiteSpace(FPuntoReorden.Text))
            {
                if (int.TryParse(FPuntoReorden.Text.Trim(), out var pr) && pr >= 0)
                    puntoReorden = pr;
                else
                    errores.Add("• Punto de reorden (debe ser un numero entero >= 0)");
            }

            // Tallas: filtrar las que tienen talla vacia, validar duplicados y stock
            var tallasNoVacias = _tallas
                .Where(t => !string.IsNullOrWhiteSpace(t.Talla))
                .ToList();

            if (tallasNoVacias.Count == 0)
            {
                errores.Add("• Debes capturar al menos una talla con su stock");
            }
            else
            {
                var duplicados = tallasNoVacias
                    .GroupBy(t => t.Talla.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicados.Count > 0)
                    errores.Add($"• Tallas duplicadas: {string.Join(", ", duplicados)}");

                if (tallasNoVacias.Any(t => t.StockActual < 0))
                    errores.Add("• El stock de cada talla no puede ser negativo");
            }

            if (errores.Count > 0)
            {
                MessageBox.Show(
                    $"Por favor corrige lo siguiente:\n\n{string.Join("\n", errores)}",
                    "Campos requeridos",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Construir el producto
            producto.Nombre        = FNombre.Text.Trim();
            producto.Marca         = FMarca.Text?.Trim() ?? string.Empty;
            producto.Modelo        = FModelo.Text?.Trim() ?? string.Empty;
            producto.Color         = FColor.Text?.Trim() ?? string.Empty;
            producto.Precio        = precio;
            producto.CostoUnitario = costo;
            producto.PuntoReorden  = puntoReorden;
            producto.Estado        = (FEstado.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Activo";

            var catSel = FCategoria.SelectedItem as Categoria;
            producto.CategoriaId = (catSel?.Id ?? 0) > 0 ? catSel!.Id : null;

            var provSel = FProveedor.SelectedItem as Proveedor;
            producto.ProveedorId = (provSel?.Id ?? 0) > 0 ? provSel!.Id : null;

            tallasValidas = tallasNoVacias;
            return true;
        }
    }

    /// <summary>
    /// ViewModel temporal para editar tallas en la UI. No se guarda directamente:
    /// se convierte a TallaInventario al hacer "Guardar".
    /// Implementa INotifyPropertyChanged para que el TextBox del stock notifique cambios
    /// y se actualice el total.
    /// </summary>
    public class TallaEditableViewModel : INotifyPropertyChanged
    {
        private string _talla;
        private int _stockActual;

        public TallaEditableViewModel(string talla, int stockActual)
        {
            _talla = talla;
            _stockActual = stockActual;
        }

        public string Talla
        {
            get => _talla;
            set { _talla = value ?? string.Empty; OnPropertyChanged(nameof(Talla)); }
        }

        public int StockActual
        {
            get => _stockActual;
            set { _stockActual = value; OnPropertyChanged(nameof(StockActual)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string p)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
