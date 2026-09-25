using System.Windows;
using System.Windows.Controls;
using ZarelyPOS.Helpers;
using ZarelyPOS.Models;
using ZarelyPOS.Services;

namespace ZarelyPOS.Views
{
    /// <summary>
    /// Reportes y estadisticas en un rango de fechas: KPIs, desglose por metodo de pago,
    /// grafica de ventas por dia, productos mas vendidos y alerta de stock bajo. Exporta a PDF.
    /// </summary>
    public partial class ReportesWindow : Window
    {
        private ReporteResumen? _reporte;

        public ReportesWindow()
        {
            InitializeComponent();

            TxtUsuarioHeader.Text = SessionManager.UsuarioActual is { } u
                ? $"{u.PrimerNombre} {u.PrimerApellido}"
                : string.Empty;

            // Rango por defecto: mes en curso.
            var hoy = DateTime.Today;
            DpDesde.SelectedDate = new DateTime(hoy.Year, hoy.Month, 1);
            DpHasta.SelectedDate = hoy;

            Loaded += async (_, _) => await CalcularAsync();
        }

        private async Task CalcularAsync()
        {
            var desde = DpDesde.SelectedDate ?? DateTime.Today;
            var hasta = DpHasta.SelectedDate ?? DateTime.Today;
            if (hasta < desde) (desde, hasta) = (hasta, desde);

            var ventas = await App.Db.GetVentasAsync();
            var productos = await App.Db.GetProductosAsync();

            _reporte = ReporteResumen.DesdeDatos(desde, hasta, ventas, productos);
            PintarReporte(_reporte);
        }

        private void PintarReporte(ReporteResumen r)
        {
            TxtRango.Text = $"Del {r.Desde:dd/MM/yyyy} al {r.Hasta:dd/MM/yyyy}";

            // KPIs
            TxtKpiTotal.Text      = r.TotalVendido.ToString("C2");
            TxtKpiVentas.Text     = r.NumVentas.ToString();
            TxtKpiTicket.Text     = r.TicketPromedio.ToString("C2");
            TxtKpiUnidades.Text   = r.UnidadesVendidas.ToString();
            TxtKpiDescuentos.Text = r.Descuentos > 0 ? $"-{r.Descuentos:C2}" : "$0.00";

            // Metodo de pago
            TxtRepEfectivoLbl.Text = $"Efectivo ({r.NumEfectivo})";
            TxtRepEfectivo.Text    = $"{r.TotalEfectivo:C2}  ·  {r.PctEfectivo:0}%";
            TxtRepTarjetaLbl.Text  = $"Tarjeta ({r.NumTarjeta})";
            TxtRepTarjeta.Text     = $"{r.TotalTarjeta:C2}  ·  {r.PctTarjeta:0}%";

            ColEfectivo.Width      = new GridLength(r.PctEfectivo, GridUnitType.Star);
            ColEfectivoResto.Width = new GridLength(Math.Max(0, 100 - r.PctEfectivo), GridUnitType.Star);
            ColTarjeta.Width       = new GridLength(r.PctTarjeta, GridUnitType.Star);
            ColTarjetaResto.Width  = new GridLength(Math.Max(0, 100 - r.PctTarjeta), GridUnitType.Star);

            // Grafica de ventas por dia
            var maxTotal = r.VentasPorDia.Count > 0 ? r.VentasPorDia.Max(d => d.Total) : 0m;
            var barras = r.VentasPorDia.Select(d => new BarraDia
            {
                Etiqueta   = d.Etiqueta,
                TotalCorto = d.Total > 0 ? Corto(d.Total) : string.Empty,
                AlturaPx   = (maxTotal > 0 && d.Total > 0)
                    ? Math.Max(4.0, (double)(d.Total / maxTotal) * 150.0)
                    : 0.0
            }).ToList();

            IcVentasDia.ItemsSource = barras;
            var hayGrafica = barras.Any(b => b.AlturaPx > 0);
            IcVentasDia.Visibility  = hayGrafica ? Visibility.Visible : Visibility.Collapsed;
            TxtSinGrafica.Visibility = hayGrafica ? Visibility.Collapsed : Visibility.Visible;

            // Top productos (10)
            var top = r.TopProductos.Take(10).ToList();
            GridTop.ItemsSource = top;
            GridTop.Visibility  = top.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtSinTop.Visibility = top.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

            // Stock bajo
            GridStock.ItemsSource = r.StockBajo;
            GridStock.Visibility  = r.StockBajo.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtSinStock.Visibility = r.StockBajo.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        // Formato compacto para las etiquetas de la grafica ($1.2k, $850).
        private static string Corto(decimal v)
            => v >= 1000 ? $"${v / 1000:0.#}k" : $"${v:0}";

        // ─── Rango: botones rapidos ───
        private async void BtnCalcular_Click(object sender, RoutedEventArgs e) => await CalcularAsync();

        private async void BtnHoy_Click(object sender, RoutedEventArgs e)
        {
            DpDesde.SelectedDate = DateTime.Today;
            DpHasta.SelectedDate = DateTime.Today;
            await CalcularAsync();
        }

        private async void Btn7_Click(object sender, RoutedEventArgs e)
        {
            DpHasta.SelectedDate = DateTime.Today;
            DpDesde.SelectedDate = DateTime.Today.AddDays(-6);
            await CalcularAsync();
        }

        private async void BtnMes_Click(object sender, RoutedEventArgs e)
        {
            var hoy = DateTime.Today;
            DpDesde.SelectedDate = new DateTime(hoy.Year, hoy.Month, 1);
            DpHasta.SelectedDate = hoy;
            await CalcularAsync();
        }

        private async void BtnTodo_Click(object sender, RoutedEventArgs e)
        {
            DpDesde.SelectedDate = new DateTime(2020, 1, 1);
            DpHasta.SelectedDate = DateTime.Today;
            await CalcularAsync();
        }

        private void BtnExportarPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_reporte == null) return;
            try
            {
                var usuario = SessionManager.UsuarioActual is { } u
                    ? $"{u.PrimerNombre} {u.PrimerApellido}"
                    : "—";
                var ruta = PdfService.GenerarReporte(_reporte, usuario);
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

    /// <summary>Barra de la grafica de ventas por dia (con alto ya escalado en px).</summary>
    public class BarraDia
    {
        public string Etiqueta { get; set; } = string.Empty;
        public string TotalCorto { get; set; } = string.Empty;
        public double AlturaPx { get; set; }
    }
}
