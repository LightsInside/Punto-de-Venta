using System.Windows;
using System.Windows.Controls;
using ZarelyPOS.Helpers;
using ZarelyPOS.Models;
using ZarelyPOS.Services;

namespace ZarelyPOS.Views
{
    /// <summary>
    /// Historial de ventas: lista todas las ventas registradas (por folio), permite
    /// buscar por folio o cliente, ver el detalle/ticket de cada una y eliminarlas.
    /// Al eliminar una venta "Completado" el stock se reintegra automaticamente
    /// (la logica vive en DatabaseService.EliminarVentaAsync).
    /// </summary>
    public partial class HistorialVentasWindow : Window
    {
        // Copia en memoria de todas las ventas; el buscador filtra sobre esta lista
        // sin volver a pegarle a la base de datos en cada tecla.
        private List<Venta> _todas = new();

        // Venta que se esta mostrando en el overlay de detalle (para reimprimir su PDF).
        private Venta? _ventaDetalle;

        public HistorialVentasWindow()
        {
            InitializeComponent();

            TxtUsuarioHeader.Text = SessionManager.UsuarioActual is { } u
                ? $"{u.PrimerNombre} {u.PrimerApellido}"
                : string.Empty;

            Loaded += async (_, _) => await CargarAsync();
        }

        private async Task CargarAsync()
        {
            // GetVentasAsync ya incluye los Detalles y ordena por fecha (mas reciente primero).
            _todas = await App.Db.GetVentasAsync();
            AplicarFiltro();
        }

        private void AplicarFiltro()
        {
            if (GridVentas == null) return;

            IEnumerable<Venta> filtradas = _todas;

            // Filtro por dia (solo si hay una fecha seleccionada)
            if (DpFecha.SelectedDate is DateTime dia)
                filtradas = filtradas.Where(v => v.Fecha.Date == dia.Date);

            // Filtro por texto (folio o cliente)
            var q = TxtBuscar.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(q))
            {
                filtradas = filtradas.Where(v =>
                    v.Id.ToString().Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    $"V-{v.Id:D4}".Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (v.ClienteNombre ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            var lista = filtradas.ToList();
            GridVentas.ItemsSource = lista;
            TxtConteo.Text = $"{lista.Count} venta(s)";
            TxtTotalMostrado.Text = lista.Sum(v => v.Total).ToString("C2");
        }

        private void Filtros_Changed(object sender, TextChangedEventArgs e) => AplicarFiltro();

        // El DatePicker dispara SelectedDateChanged con SelectionChangedEventArgs.
        private void Fecha_Changed(object sender, SelectionChangedEventArgs e) => AplicarFiltro();

        // "Todas": quita el filtro de dia (poner null dispara Fecha_Changed -> AplicarFiltro).
        private void BtnTodas_Click(object sender, RoutedEventArgs e)
        {
            if (DpFecha.SelectedDate == null) AplicarFiltro();
            else DpFecha.SelectedDate = null;
        }

        // ─────────────────────────────────────────────────────────────
        //  VER DETALLE (overlay tipo ticket)
        // ─────────────────────────────────────────────────────────────
        private void BtnDetalles_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not Venta venta) return;
            MostrarDetalle(venta);
        }

        private void MostrarDetalle(Venta venta)
        {
            _ventaDetalle = venta;
            TxtDetalleTitulo.Text = $"Detalle de venta — Folio V-{venta.Id:D4}";
            DTxtFolio.Text    = $"V-{venta.Id:D4}";
            DTxtFecha.Text    = venta.FechaTexto;
            DTxtCliente.Text  = venta.ClienteNombre;
            DTxtVendedor.Text = string.IsNullOrWhiteSpace(venta.UsuarioNombre) ? "—" : venta.UsuarioNombre;
            DTxtMetodo.Text   = venta.MetodoPago;

            // El renglon "Recibido / Cambio" solo aplica a pagos en efectivo.
            if (venta.MetodoPago == "Efectivo" && venta.DineroRecibido is decimal recibido)
            {
                DTxtEfectivo.Text = $"Recibido: {recibido:C2}     ·     Cambio: {(venta.Cambio ?? 0m):C2}";
                DTxtEfectivo.Visibility = Visibility.Visible;
            }
            else
            {
                DTxtEfectivo.Visibility = Visibility.Collapsed;
            }

            GridDetalle.ItemsSource = venta.Detalles;
            DTxtTotal.Text = venta.Total.ToString("C2");
            OverlayDetalle.Visibility = Visibility.Visible;
        }

        private void BtnCerrarDetalle_Click(object sender, RoutedEventArgs e)
            => OverlayDetalle.Visibility = Visibility.Collapsed;

        // Reimprime/genera el ticket PDF de la venta mostrada en el detalle.
        private void BtnTicketPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_ventaDetalle == null) return;
            try
            {
                var ruta = PdfService.GenerarTicket(_ventaDetalle);
                PdfService.Abrir(ruta);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo generar el PDF:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  ELIMINAR VENTA
        //  Solo admins. Al confirmar, el stock se reintegra (si seguia "Completado").
        // ─────────────────────────────────────────────────────────────
        private async void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not Venta venta) return;

            // Eliminar una venta es destructivo y afecta inventario: solo administradores.
            if (!SessionManager.EsAdmin)
            {
                MessageBox.Show(
                    "Solo un administrador puede eliminar ventas.",
                    "Acceso denegado",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var aviso = venta.Estado == "Completado"
                ? $"¿Eliminar la venta V-{venta.Id:D4} por {venta.Total:C2}?\n\n" +
                  "El stock de los productos vendidos se DEVOLVERA al inventario."
                : $"¿Eliminar la venta V-{venta.Id:D4} por {venta.Total:C2}?";

            var r = MessageBox.Show(
                aviso + "\n\nEsta accion no se puede deshacer.",
                "Eliminar venta",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (r != MessageBoxResult.Yes) return;

            try
            {
                var eliminada = await App.Db.EliminarVentaAsync(venta.Id);

                if (!eliminada)
                {
                    MessageBox.Show(
                        "La venta ya no existe (es posible que se eliminara desde otra ventana).",
                        "Zarely POS",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Si el overlay mostraba justo esta venta, lo cerramos para no dejar
                // datos "fantasma" de una venta que ya no existe.
                OverlayDetalle.Visibility = Visibility.Collapsed;

                await CargarAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudo eliminar la venta:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRegresar_Click(object sender, RoutedEventArgs e) => Close();
    }
}
