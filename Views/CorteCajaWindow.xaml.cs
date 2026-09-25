using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ZarelyPOS.Helpers;
using ZarelyPOS.Models;
using ZarelyPOS.Services;

namespace ZarelyPOS.Views
{
    /// <summary>
    /// Corte de caja: resume las ventas "Completado" de un dia (totales, desglose por
    /// metodo de pago, descuentos y efectivo esperado en caja) y permite exportarlo a PDF.
    /// </summary>
    public partial class CorteCajaWindow : Window
    {
        private CorteCajaResumen? _resumen;

        public CorteCajaWindow()
        {
            InitializeComponent();

            TxtUsuarioHeader.Text = SessionManager.UsuarioActual is { } u
                ? $"{u.PrimerNombre} {u.PrimerApellido}"
                : string.Empty;

            DpFecha.SelectedDate = DateTime.Today;
            Loaded += async (_, _) => await CalcularAsync();
        }

        private decimal ObtenerFondo()
            => decimal.TryParse(FFondo.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var f) && f > 0
                ? f
                : 0m;

        private async Task CalcularAsync()
        {
            var dia = DpFecha.SelectedDate ?? DateTime.Today;

            var ventas = (await App.Db.GetVentasPorFechaAsync(dia))
                .Where(v => v.Estado == "Completado")
                .ToList();

            _resumen = CorteCajaResumen.DesdeVentas(dia, ventas, ObtenerFondo());
            PintarResumen(_resumen);
        }

        private void PintarResumen(CorteCajaResumen c)
        {
            TxtResumenFecha.Text   = c.Dia.ToString("dddd, dd 'de' MMMM 'de' yyyy", new CultureInfo("es-MX"));
            TxtNumVentas.Text      = c.NumVentas.ToString();
            TxtSubtotal.Text       = c.SubtotalBruto.ToString("C2");
            TxtDescuentos.Text     = c.Descuentos > 0 ? $"-{c.Descuentos:C2}" : "$0.00";
            TxtTotalVendido.Text   = c.TotalVendido.ToString("C2");
            TxtTicketProm.Text     = c.TicketPromedio.ToString("C2");

            TxtEfectivoLbl.Text    = $"Efectivo  ({c.NumEfectivo} venta(s))";
            TxtEfectivo.Text       = c.TotalEfectivo.ToString("C2");
            TxtTarjetaLbl.Text     = $"Tarjeta  ({c.NumTarjeta} venta(s))";
            TxtTarjeta.Text        = c.TotalTarjeta.ToString("C2");

            TxtFondo.Text          = c.Fondo.ToString("C2");
            TxtEfectivoCaja.Text   = c.EfectivoEnCaja.ToString("C2");

            GridProductos.ItemsSource  = c.Productos;
            TxtUnidades.Text           = $"{c.UnidadesVendidas} par(es)";
            GridProductos.Visibility   = c.Productos.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            TxtSinProductos.Visibility = c.Productos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void BtnCalcular_Click(object sender, RoutedEventArgs e) => await CalcularAsync();

        // El DatePicker dispara este evento; recalculamos solo si la ventana ya cargo.
        private async void Filtros_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) await CalcularAsync();
        }

        private void BtnExportarPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_resumen == null) return;

            if (_resumen.NumVentas == 0)
            {
                var seguir = MessageBox.Show(
                    "No hay ventas registradas en este dia. ¿Generar el corte de todas formas?",
                    "Corte de caja", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (seguir != MessageBoxResult.Yes) return;
            }

            try
            {
                var usuario = SessionManager.UsuarioActual is { } u
                    ? $"{u.PrimerNombre} {u.PrimerApellido}"
                    : "—";

                var ruta = PdfService.GenerarCorte(_resumen, usuario);
                PdfService.Abrir(ruta);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo generar el PDF:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRegresar_Click(object sender, RoutedEventArgs e) => Close();
    }
}
