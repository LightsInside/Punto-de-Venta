using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ZarelyPOS.Helpers;
using ZarelyPOS.Models;
using ZarelyPOS.Services;

namespace ZarelyPOS.Views
{
    public partial class VentasWindow : Window
    {
        // ─── Estado ───
        private List<Producto> _productos = new();
        private readonly ObservableCollection<CarritoItem> _carrito = new();

        // Producto elegido desde las sugerencias en vivo.
        private Producto? _productoSeleccionado;

        // Bandera para ignorar eventos que se disparan durante la carga inicial
        private bool _inicializado = false;

        public VentasWindow()
        {
            InitializeComponent();
            ConfigurarHeader();
            GridCarrito.ItemsSource = _carrito;
            this.Loaded += VentasWindow_Loaded;
        }

        private async void VentasWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            this.Loaded -= VentasWindow_Loaded;
            await CargarClientesAsync();
            await CargarProductosAsync();
            _inicializado = true;
        }

        // ─── Inicializacion ──────────────────────────────────────

        private void ConfigurarHeader()
        {
            var u = SessionManager.UsuarioActual!;
            TxtUsuarioHeader.Text = $"{u.PrimerNombre} {u.PrimerApellido}";
        }

        private async Task CargarClientesAsync()
        {
            var activos = (await App.Db.GetClientesAsync())
                .Where(c => c.Activo)
                .ToList();

            var opciones = new List<ClienteOption>
            {
                new(null, "Publico en general")
            };
            opciones.AddRange(activos.Select(c => new ClienteOption(c, c.NombreCompleto)));

            CmbCliente.ItemsSource = opciones;
            CmbCliente.SelectedIndex = 0;
        }

        private async Task CargarProductosAsync()
        {
            // Solo productos activos y con al menos una talla con stock
            _productos = (await App.Db.GetProductosAsync())
                .Where(p => p.Estado == "Activo")
                .ToList();

            AplicarFiltroProductos();
        }

        // ─── Filtro / busqueda de productos ──────────────────────

        private void TxtBuscarProducto_TextChanged(object sender, TextChangedEventArgs e)
            => AplicarFiltroProductos();

        private void AplicarFiltroProductos()
        {
            if (LstSugerencias == null) return;

            var busqueda = (TxtBuscarProducto?.Text ?? string.Empty).Trim().ToLowerInvariant();

            // Sin texto: no mostramos sugerencias.
            if (string.IsNullOrEmpty(busqueda))
            {
                LstSugerencias.ItemsSource = null;
                LstSugerencias.Visibility = Visibility.Collapsed;
                return;
            }

            var filtrados = _productos.Where(p =>
                p.Nombre.ToLowerInvariant().Contains(busqueda)
                || p.Marca.ToLowerInvariant().Contains(busqueda)
                || p.Modelo.ToLowerInvariant().Contains(busqueda)
                || p.Color.ToLowerInvariant().Contains(busqueda)
            ).ToList();

            LstSugerencias.ItemsSource = filtrados
                .Select(p => new ProductoOption(p, $"{p.Marca} {p.Nombre} ({p.Color})  —  {p.Precio:C2}"))
                .ToList();

            // Mostramos la lista solo si hay coincidencias.
            LstSugerencias.Visibility = filtrados.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ─── Seleccion de producto / talla ───────────────────────

        private void LstSugerencias_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstSugerencias.SelectedItem is not ProductoOption opcion) return;
            SeleccionarProducto(opcion.Producto);
        }

        private void SeleccionarProducto(Producto prod)
        {
            _productoSeleccionado = prod;
            TxtProductoSeleccionado.Text = $"{prod.Marca} {prod.Nombre} ({prod.Color})  —  {prod.Precio:C2}";

            // Cargar tallas mostrando el stock disponible de cada una
            CmbTalla.ItemsSource = prod.Tallas
                .OrderBy(t => t.Talla)
                .Select(t => new TallaOption(t, $"{t.Talla}   (stock: {t.StockActual})"))
                .ToList();
            CmbTalla.SelectedIndex = -1;

            // Precio sugerido (editable por si hay descuento manual)
            FPrecio.Text = prod.Precio.ToString("0.00");
            FCantidad.Text = "1";

            // Ocultar sugerencias tras elegir
            LstSugerencias.Visibility = Visibility.Collapsed;

            ActualizarHintYValidacion();
        }

        private void CmbTalla_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => ActualizarHintYValidacion();

        private void Recalcular_Changed(object sender, TextChangedEventArgs e)
            => ActualizarHintYValidacion();

        /// <summary>
        /// Actualiza el texto de stock disponible y valida en vivo que la cantidad
        /// solicitada no exceda el stock (descontando lo que ya esta en el carrito).
        /// Devuelve true si la seleccion actual es valida para agregar.
        /// </summary>
        private bool ActualizarHintYValidacion()
        {
            if (!IsLoaded || PanelStockError == null) return false;

            OcultarError();

            if (CmbTalla?.SelectedItem is not TallaOption tallaOpt)
            {
                TxtStockHint.Text = string.Empty;
                return false;
            }

            var talla = tallaOpt.Talla;
            var enCarrito = _carrito
                .Where(i => i.TallaInventarioId == talla.Id)
                .Sum(i => i.Cantidad);

            var disponible = talla.StockActual - enCarrito;
            TxtStockHint.Text = $"Disponible para vender: {disponible} " +
                                $"(stock {talla.StockActual}, en carrito {enCarrito}).";

            if (!int.TryParse(FCantidad.Text, out var cantidad) || cantidad <= 0)
                return false;

            if (!decimal.TryParse(FPrecio.Text, out var precio) || precio <= 0)
                return false;

            if (cantidad > disponible)
            {
                MostrarError($"No hay suficiente stock. Disponible: {disponible} unidad(es). " +
                             $"Estas intentando agregar {cantidad}.");
                return false;
            }

            return true;
        }

        // ─── Agregar al carrito ──────────────────────────────────

        private void BtnAgregar_Click(object sender, RoutedEventArgs e)
        {
            if (_productoSeleccionado is null)
            {
                MostrarError("Busca y selecciona un producto.");
                return;
            }
            if (CmbTalla.SelectedItem is not TallaOption tallaOpt)
            {
                MostrarError("Selecciona una talla.");
                return;
            }
            if (!int.TryParse(FCantidad.Text, out var cantidad) || cantidad <= 0)
            {
                MostrarError("Captura una cantidad valida (mayor a 0).");
                return;
            }
            if (!decimal.TryParse(FPrecio.Text, out var precio) || precio <= 0)
            {
                MostrarError("Captura un precio unitario valido (mayor a 0).");
                return;
            }

            // Revalidar stock contra lo que ya esta en el carrito
            if (!ActualizarHintYValidacion()) return;

            var prod = _productoSeleccionado;
            var talla = tallaOpt.Talla;

            // Si ya existe el mismo producto+talla+precio en el carrito, sumamos cantidad
            var existente = _carrito.FirstOrDefault(i =>
                i.TallaInventarioId == talla.Id && i.PrecioUnitario == precio);

            if (existente != null)
            {
                existente.Cantidad += cantidad;
                existente.Subtotal = existente.Cantidad * existente.PrecioUnitario;
                GridCarrito.Items.Refresh();
            }
            else
            {
                _carrito.Add(new CarritoItem
                {
                    ProductoId        = prod.Id,
                    TallaInventarioId = talla.Id,
                    ProductoNombre    = $"{prod.Marca} {prod.Nombre}",
                    Talla             = talla.Talla,
                    Cantidad          = cantidad,
                    PrecioUnitario    = precio,
                    Subtotal          = cantidad * precio
                });
            }

            ActualizarTotales();

            // Dejar listo para el siguiente producto
            FCantidad.Text = "1";
            TxtBuscarProducto.Text = string.Empty;
            _productoSeleccionado = null;
            TxtProductoSeleccionado.Text = "Ninguno";
            CmbTalla.ItemsSource = null;
            LstSugerencias.Visibility = Visibility.Collapsed;
            TxtStockHint.Text = string.Empty;
            TxtBuscarProducto.Focus();
        }

        private void BtnQuitarItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CarritoItem item)
            {
                _carrito.Remove(item);
                ActualizarTotales();
            }
        }

        private void BtnVaciar_Click(object sender, RoutedEventArgs e)
        {
            if (_carrito.Count == 0) return;

            var r = MessageBox.Show("¿Vaciar el carrito? Se quitaran todos los productos.",
                "Vaciar carrito", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;

            _carrito.Clear();
            ActualizarTotales();
        }

        private void ActualizarTotales()
        {
            // Guard: puede dispararse durante la carga inicial antes de crear los controles.
            if (TxtTotal == null || TxtSubtotal == null) return;

            var totalArticulos = _carrito.Sum(i => i.Cantidad);
            var subtotal = CalcularSubtotal();
            var descuento = CalcularDescuentoMonto();
            var total = subtotal - descuento;

            TxtConteoCarrito.Text   = _carrito.Count.ToString();
            TxtTotalArticulos.Text  = totalArticulos.ToString();
            TxtSubtotal.Text        = subtotal.ToString("C2");
            TxtDescuentoMonto.Text  = descuento > 0 ? $"-{descuento:C2}" : "$0.00";
            TxtTotal.Text           = total.ToString("C2");
        }

        // Suma de los renglones del carrito, antes de aplicar descuento.
        private decimal CalcularSubtotal() => _carrito.Sum(i => i.Subtotal);

        // Porcentaje capturado en el cuadro de descuento, acotado a [0, 100].
        private decimal ObtenerDescuentoPct()
        {
            if (FDescuento == null || !decimal.TryParse(FDescuento.Text, out var pct))
                return 0m;
            return Math.Clamp(pct, 0m, 100m);
        }

        // Monto en dinero que representa el descuento, redondeado a 2 decimales.
        private decimal CalcularDescuentoMonto()
            => Math.Round(CalcularSubtotal() * ObtenerDescuentoPct() / 100m, 2);

        // Total final a cobrar = subtotal - descuento.
        private decimal CalcularTotal() => CalcularSubtotal() - CalcularDescuentoMonto();

        private void FDescuento_TextChanged(object sender, TextChangedEventArgs e)
            => ActualizarTotales();

        // ─── Cobro (overlay) ─────────────────────────────────────

        private void BtnCobrar_Click(object sender, RoutedEventArgs e)
        {
            if (_carrito.Count == 0)
            {
                MessageBox.Show("El carrito esta vacio. Agrega al menos un producto.",
                    "Zarely POS", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TxtCobroTotal.Text   = CalcularTotal().ToString("C2");
            CmbMetodoPago.SelectedIndex = 0; // Efectivo
            FDineroRecibido.Text = string.Empty;
            TxtCambio.Text       = "$0.00";
            PanelEfectivo.Visibility = Visibility.Visible;

            OverlayCobro.Visibility = Visibility.Visible;
            FDineroRecibido.Focus();
        }

        private void CmbMetodoPago_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PanelEfectivo == null) return; // aun no inicializa

            var metodo = (CmbMetodoPago.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Efectivo";
            PanelEfectivo.Visibility = metodo == "Efectivo" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void FDineroRecibido_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtCambio == null) return;

            var total = CalcularTotal();
            if (decimal.TryParse(FDineroRecibido.Text, out var recibido) && recibido >= total)
                TxtCambio.Text = (recibido - total).ToString("C2");
            else
                TxtCambio.Text = "$0.00";
        }

        private void BtnCancelarCobro_Click(object sender, RoutedEventArgs e)
            => OverlayCobro.Visibility = Visibility.Collapsed;

        private async void BtnConfirmarCobro_Click(object sender, RoutedEventArgs e)
        {
            var subtotal = CalcularSubtotal();
            var descuentoPct = ObtenerDescuentoPct();
            var descuentoMonto = CalcularDescuentoMonto();
            var total = subtotal - descuentoMonto;
            var metodo = (CmbMetodoPago.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Efectivo";

            decimal? dineroRecibido = null;
            decimal? cambio = null;

            if (metodo == "Efectivo")
            {
                if (!decimal.TryParse(FDineroRecibido.Text, out var recibido) || recibido < total)
                {
                    MessageBox.Show(
                        $"El dinero recibido es menor que el total a pagar ({total:C2}).",
                        "Pago insuficiente", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                dineroRecibido = recibido;
                cambio = recibido - total;
            }

            // Construir la venta con sus detalles (snapshots)
            var clienteOpt = CmbCliente.SelectedItem as ClienteOption;
            var usuario = SessionManager.UsuarioActual;

            var venta = new Venta
            {
                ClienteId      = clienteOpt?.Cliente?.Id,
                ClienteNombre  = clienteOpt?.Cliente?.NombreCompleto ?? "Publico en general",
                Fecha          = DateTime.Now,
                Subtotal       = subtotal,
                DescuentoPorcentaje = descuentoPct,
                DescuentoMonto = descuentoMonto,
                Total          = total,
                MetodoPago     = metodo,
                DineroRecibido = dineroRecibido,
                Cambio         = cambio,
                Estado         = "Completado",
                UsuarioId      = usuario?.Id,
                UsuarioNombre  = usuario != null ? $"{usuario.PrimerNombre} {usuario.PrimerApellido}" : string.Empty,
                Detalles       = _carrito.Select(i => new DetalleVenta
                {
                    ProductoId        = i.ProductoId,
                    TallaInventarioId = i.TallaInventarioId,
                    ProductoNombre    = i.ProductoNombre,
                    Talla             = i.Talla,
                    Cantidad          = i.Cantidad,
                    PrecioUnitario    = i.PrecioUnitario,
                    Subtotal          = i.Subtotal
                }).ToList()
            };

            try
            {
                BtnConfirmarCobro.IsEnabled = false;
                var registrada = await App.Db.RegistrarVentaAsync(venta);

                MostrarTicket(registrada);
                OfrecerTicketPdf(registrada);

                // Limpiar y refrescar stock
                _carrito.Clear();
                if (FDescuento != null) FDescuento.Text = "0";
                ActualizarTotales();
                OverlayCobro.Visibility = Visibility.Collapsed;
                await CargarProductosAsync();
            }
            catch (InvalidOperationException ex)
            {
                // Stock cambio entre que se armo el carrito y se confirmo
                MessageBox.Show(ex.Message, "No se pudo completar la venta",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al registrar la venta: {ex.Message}",
                    "Zarely POS", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnConfirmarCobro.IsEnabled = true;
            }
        }

        // ─── Ticket ──────────────────────────────────────────────

        private void MostrarTicket(Venta venta)
        {
            var sb = new StringBuilder();
            sb.AppendLine("        ZARELY  —  Zapateria");
            sb.AppendLine("══════════════════════════════════");
            sb.AppendLine($"Venta #: {venta.Id}");
            sb.AppendLine($"Fecha:   {venta.Fecha:dd/MM/yyyy HH:mm}");
            sb.AppendLine($"Cliente: {venta.ClienteNombre}");
            sb.AppendLine($"Atendio: {venta.UsuarioNombre}");
            sb.AppendLine("──────────────────────────────────");

            foreach (var d in venta.Detalles)
            {
                sb.AppendLine($"{d.Cantidad} x {d.ProductoNombre} (T{d.Talla})");
                sb.AppendLine($"      {d.PrecioUnitario:C2} c/u        {d.Subtotal:C2}");
            }

            sb.AppendLine("──────────────────────────────────");
            if (venta.DescuentoMonto > 0)
            {
                sb.AppendLine($"Subtotal:        {venta.Subtotal:C2}");
                sb.AppendLine($"Descuento ({venta.DescuentoPorcentaje:0.##}%): -{venta.DescuentoMonto:C2}");
            }
            sb.AppendLine($"TOTAL:           {venta.Total:C2}");
            sb.AppendLine($"Metodo de pago:  {venta.MetodoPago}");
            if (venta.MetodoPago == "Efectivo")
            {
                sb.AppendLine($"Recibido:        {venta.DineroRecibido:C2}");
                sb.AppendLine($"Cambio:          {venta.Cambio:C2}");
            }
            sb.AppendLine("══════════════════════════════════");
            sb.AppendLine("       ¡Gracias por su compra!");

            MessageBox.Show(sb.ToString(), "Venta registrada — Ticket",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>Pregunta si se quiere generar el ticket en PDF y, si si, lo crea y lo abre.</summary>
        private void OfrecerTicketPdf(Venta venta)
        {
            var r = MessageBox.Show("¿Generar el ticket en PDF?", "Ticket PDF",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;

            try
            {
                var ruta = PdfService.GenerarTicket(venta);
                PdfService.Abrir(ruta);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo generar el PDF:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Helpers de error ────────────────────────────────────

        private void MostrarError(string mensaje)
        {
            TxtStockError.Text = mensaje;
            PanelStockError.Visibility = Visibility.Visible;
        }

        private void OcultarError() => PanelStockError.Visibility = Visibility.Collapsed;

        private void BtnRegresar_Click(object sender, RoutedEventArgs e) => Close();
    }

    // ═══════════════════════════════════════════════════════════
    //  Tipos auxiliares (solo para la UI de ventas)
    // ═══════════════════════════════════════════════════════════

    /// <summary>Renglon del carrito en memoria (antes de convertirse en DetalleVenta).</summary>
    public class CarritoItem
    {
        public int ProductoId { get; set; }
        public int? TallaInventarioId { get; set; }
        public string ProductoNombre { get; set; } = string.Empty;
        public string Talla { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }

    // Opciones para los ComboBox (texto a mostrar + objeto real seleccionado)
    public record ProductoOption(Producto Producto, string Display);
    public record TallaOption(TallaInventario Talla, string Display);
    public record ClienteOption(Cliente? Cliente, string Display);
}
