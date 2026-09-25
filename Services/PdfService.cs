using System.Diagnostics;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ZarelyPOS.Models;

namespace ZarelyPOS.Services
{
    /// <summary>
    /// Genera PDFs con QuestPDF: el ticket de una venta (formato angosto, listo para
    /// una futura impresora termica de 80mm) y el corte de caja del dia (formato carta).
    /// Los archivos se guardan en  Documentos\ZarelyPOS\  y el metodo devuelve la ruta.
    /// </summary>
    public static class PdfService
    {
        static PdfService()
        {
            // Licencia Community: gratis para negocios con menos de 1M USD de ingresos
            // anuales, sin fines de lucro y proyectos open-source. Se fija una sola vez.
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // ─────────────────────────────────────────────────────────────
        //  Utilidades
        // ─────────────────────────────────────────────────────────────

        /// <summary>Colores de marca tomados del logo de Zapaterias Zarely.</summary>
        private const string AzulZarely = "#1A1DB5";
        private const string AmarilloZarely = "#F7F04E";

        private static string CarpetaSalida(string subcarpeta)
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ZarelyPOS", subcarpeta);
            Directory.CreateDirectory(dir);
            return dir;
        }

        // Cache del logo para no leer el recurso en cada PDF.
        private static byte[]? _logoColor;
        private static byte[]? _logoTicket;

        /// <summary>
        /// Carga una imagen empaquetada como Resource del ensamblado (Resources\Images\...).
        /// Devuelve null si no se encuentra, para que el PDF se genere igual sin logo.
        /// </summary>
        private static byte[]? CargarImagenRecurso(string rutaRelativa)
        {
            try
            {
                var uri = new Uri($"pack://application:,,,/{rutaRelativa}", UriKind.Absolute);
                var info = System.Windows.Application.GetResourceStream(uri);
                if (info == null) return null;

                using var ms = new MemoryStream();
                info.Stream.CopyTo(ms);
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Logo a color, para los PDFs tamano carta (corte y reportes).</summary>
        private static byte[]? LogoColor =>
            _logoColor ??= CargarImagenRecurso("Resources/Images/logo.png");

        /// <summary>Logo en blanco y negro, mas legible en impresora termica.</summary>
        private static byte[]? LogoTicket =>
            _logoTicket ??= CargarImagenRecurso("Resources/Images/logo_ticket.png");

        /// <summary>Abre un archivo con la app por defecto del sistema (visor de PDF).</summary>
        public static void Abrir(string ruta)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = ruta, UseShellExecute = true });
            }
            catch
            {
                // Si no hay visor asociado no pasa nada: el PDF quedo guardado en disco.
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  TICKET DE VENTA
        // ─────────────────────────────────────────────────────────────

        public static string GenerarTicket(Venta venta)
        {
            var ruta = Path.Combine(CarpetaSalida("Tickets"), $"Ticket_V-{venta.Id:D4}.pdf");

            Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    // Ancho de 80mm (rollo termico). Si tu version de QuestPDF no tuviera
                    // ContinuousSize, cambia esta linea por: page.Size(PageSizes.A5);
                    page.ContinuousSize(80, Unit.Millimetre);
                    page.MarginHorizontal(6, Unit.Millimetre);
                    page.MarginVertical(8, Unit.Millimetre);
                    page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Segoe UI"));

                    page.Content().Column(col =>
                    {
                        // Logo de la zapateria (si falla la carga, cae al texto de siempre)
                        var logo = LogoTicket;
                        if (logo != null)
                            col.Item().AlignCenter().Width(58, Unit.Millimetre).Image(logo);
                        else
                            col.Item().AlignCenter().Text("ZARELY").Bold().FontSize(16);

                        col.Item().AlignCenter().Text("Calzado Para Toda La Familia").FontSize(8);
                        col.Item().PaddingTop(4).LineHorizontal(0.6f);

                        col.Item().PaddingTop(4).Text($"Folio:   V-{venta.Id:D4}");
                        col.Item().Text($"Fecha:   {venta.Fecha:dd/MM/yyyy HH:mm}");
                        col.Item().Text($"Cliente: {venta.ClienteNombre}");
                        col.Item().Text($"Atendio: {venta.UsuarioNombre}");
                        col.Item().PaddingVertical(4).LineHorizontal(0.6f);

                        // Renglones de la venta
                        foreach (var d in venta.Detalles)
                        {
                            col.Item().Text($"{d.Cantidad} x {d.ProductoNombre} (T{d.Talla})");
                            col.Item().Row(r =>
                            {
                                r.RelativeItem().Text($"   {d.PrecioUnitario:C2} c/u").FontSize(8);
                                r.ConstantItem(70).AlignRight().Text($"{d.Subtotal:C2}");
                            });
                        }

                        col.Item().PaddingVertical(4).LineHorizontal(0.6f);

                        // Totales
                        if (venta.DescuentoMonto > 0)
                        {
                            LineaTotal(col, "Subtotal:", venta.Subtotal.ToString("C2"));
                            LineaTotal(col, $"Descuento ({venta.DescuentoPorcentaje:0.##}%):",
                                       $"-{venta.DescuentoMonto:C2}");
                        }
                        LineaTotal(col, "TOTAL:", venta.Total.ToString("C2"), destacado: true);
                        LineaTotal(col, "Pago:", venta.MetodoPago);

                        if (venta.MetodoPago == "Efectivo")
                        {
                            LineaTotal(col, "Recibido:", (venta.DineroRecibido ?? 0m).ToString("C2"));
                            LineaTotal(col, "Cambio:", (venta.Cambio ?? 0m).ToString("C2"));
                        }

                        col.Item().PaddingTop(10).AlignCenter().Text("¡Gracias por su compra!");
                    });
                });
            }).GeneratePdf(ruta);

            return ruta;
        }

        private static void LineaTotal(ColumnDescriptor col, string etiqueta, string valor, bool destacado = false)
        {
            col.Item().Row(r =>
            {
                var lbl = r.RelativeItem().Text(etiqueta);
                var val = r.ConstantItem(90).AlignRight().Text(valor);
                if (destacado)
                {
                    lbl.Bold().FontSize(11);
                    val.Bold().FontSize(13);
                }
            });
        }

        // ─────────────────────────────────────────────────────────────
        //  CORTE DE CAJA
        // ─────────────────────────────────────────────────────────────

        public static string GenerarCorte(CorteCajaResumen c, string usuario)
        {
            var ruta = Path.Combine(CarpetaSalida("Cortes"), $"Corte_{c.Dia:yyyy-MM-dd}.pdf");

            Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(t => t.FontSize(11).FontFamily("Segoe UI"));

                    page.Header().Column(h =>
                    {
                        var logo = LogoColor;
                        if (logo != null)
                            h.Item().Width(150).Image(logo);
                        else
                            h.Item().Text("ZARELY — Zapateria").Bold().FontSize(20).FontColor(AzulZarely);

                        h.Item().PaddingTop(4).Text("Corte de caja").FontSize(13).FontColor(AzulZarely);
                        h.Item().PaddingTop(4).Text($"Fecha: {c.Dia:dddd, dd/MM/yyyy}");
                        h.Item().Text($"Generado por: {usuario}  ·  {DateTime.Now:dd/MM/yyyy HH:mm}")
                                .FontSize(9).FontColor(Colors.Grey.Darken1);
                        h.Item().PaddingTop(8).Height(3).Background(AmarilloZarely);
                    });

                    page.Content().PaddingTop(12).Column(col =>
                    {
                        col.Spacing(6);

                        Fila(col, "Numero de ventas", c.NumVentas.ToString());
                        Fila(col, "Subtotal (bruto)", c.SubtotalBruto.ToString("C2"));
                        Fila(col, "Descuentos otorgados", $"-{c.Descuentos:C2}");
                        Fila(col, "TOTAL VENDIDO", c.TotalVendido.ToString("C2"), destacado: true);
                        Fila(col, "Ticket promedio", c.TicketPromedio.ToString("C2"));

                        col.Item().PaddingVertical(6).LineHorizontal(0.6f);

                        col.Item().Text("Desglose por metodo de pago").Bold().FontSize(13);
                        Fila(col, $"Efectivo  ({c.NumEfectivo} venta(s))", c.TotalEfectivo.ToString("C2"));
                        Fila(col, $"Tarjeta  ({c.NumTarjeta} venta(s))", c.TotalTarjeta.ToString("C2"));

                        col.Item().PaddingVertical(6).LineHorizontal(0.6f);

                        col.Item().Text("Caja").Bold().FontSize(13);
                        Fila(col, "Fondo inicial", c.Fondo.ToString("C2"));
                        Fila(col, "+ Ventas en efectivo", c.TotalEfectivo.ToString("C2"));
                        Fila(col, "EFECTIVO ESPERADO EN CAJA", c.EfectivoEnCaja.ToString("C2"), destacado: true);

                        // Zapatos vendidos en el dia
                        if (c.Productos.Count > 0)
                        {
                            col.Item().PaddingVertical(6).LineHorizontal(0.6f);
                            col.Item().Text($"Zapatos vendidos  ({c.UnidadesVendidas} par(es))").Bold().FontSize(13);
                            col.Item().PaddingTop(4).Table(tabla =>
                            {
                                tabla.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(5);
                                    cols.ConstantColumn(55);
                                    cols.ConstantColumn(50);
                                    cols.ConstantColumn(90);
                                });

                                tabla.Header(head =>
                                {
                                    head.Cell().Text("Producto").Bold();
                                    head.Cell().AlignRight().Text("Talla").Bold();
                                    head.Cell().AlignRight().Text("Cant.").Bold();
                                    head.Cell().AlignRight().Text("Importe").Bold();
                                });

                                foreach (var p in c.Productos)
                                {
                                    tabla.Cell().PaddingVertical(2).Text(p.Nombre);
                                    tabla.Cell().PaddingVertical(2).AlignRight().Text(p.Talla);
                                    tabla.Cell().PaddingVertical(2).AlignRight().Text(p.Cantidad.ToString());
                                    tabla.Cell().PaddingVertical(2).AlignRight().Text(p.Importe.ToString("C2"));
                                }
                            });
                        }

                        col.Item().PaddingTop(40).Row(r =>
                        {
                            r.RelativeItem().AlignCenter().Column(f =>
                            {
                                f.Item().LineHorizontal(0.6f);
                                f.Item().AlignCenter().Text("Entrega").FontSize(9);
                            });
                            r.ConstantItem(40);
                            r.RelativeItem().AlignCenter().Column(f =>
                            {
                                f.Item().LineHorizontal(0.6f);
                                f.Item().AlignCenter().Text("Recibe").FontSize(9);
                            });
                        });
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Zarely POS  ·  Corte generado el ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        t.Span($"{DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
            }).GeneratePdf(ruta);

            return ruta;
        }

        // ─────────────────────────────────────────────────────────────
        //  REPORTE DE ESTADISTICAS
        // ─────────────────────────────────────────────────────────────

        public static string GenerarReporte(ReporteResumen r, string usuario)
        {
            var ruta = Path.Combine(CarpetaSalida("Reportes"),
                $"Reporte_{r.Desde:yyyy-MM-dd}_a_{r.Hasta:yyyy-MM-dd}.pdf");

            Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(t => t.FontSize(11).FontFamily("Segoe UI"));

                    page.Header().Column(h =>
                    {
                        var logo = LogoColor;
                        if (logo != null)
                            h.Item().Width(150).Image(logo);
                        else
                            h.Item().Text("ZARELY — Zapateria").Bold().FontSize(20).FontColor(AzulZarely);

                        h.Item().PaddingTop(4).Text("Reporte de ventas y estadisticas").FontSize(13).FontColor(AzulZarely);
                        h.Item().PaddingTop(4).Text($"Periodo: {r.Desde:dd/MM/yyyy} a {r.Hasta:dd/MM/yyyy}");
                        h.Item().Text($"Generado por: {usuario}  ·  {DateTime.Now:dd/MM/yyyy HH:mm}")
                                .FontSize(9).FontColor(Colors.Grey.Darken1);
                        h.Item().PaddingTop(8).Height(3).Background(AmarilloZarely);
                    });

                    page.Content().PaddingTop(12).Column(col =>
                    {
                        col.Spacing(6);

                        col.Item().Text("Resumen").Bold().FontSize(13);
                        Fila(col, "Total vendido", r.TotalVendido.ToString("C2"), destacado: true);
                        Fila(col, "Numero de ventas", r.NumVentas.ToString());
                        Fila(col, "Ticket promedio", r.TicketPromedio.ToString("C2"));
                        Fila(col, "Pares vendidos", r.UnidadesVendidas.ToString());
                        Fila(col, "Descuentos otorgados", $"-{r.Descuentos:C2}");

                        col.Item().PaddingVertical(6).LineHorizontal(0.6f);
                        col.Item().Text("Metodo de pago").Bold().FontSize(13);
                        Fila(col, $"Efectivo ({r.NumEfectivo})", $"{r.TotalEfectivo:C2}  ({r.PctEfectivo:0}%)");
                        Fila(col, $"Tarjeta ({r.NumTarjeta})", $"{r.TotalTarjeta:C2}  ({r.PctTarjeta:0}%)");

                        // Productos mas vendidos
                        if (r.TopProductos.Count > 0)
                        {
                            col.Item().PaddingVertical(6).LineHorizontal(0.6f);
                            col.Item().Text("Productos mas vendidos").Bold().FontSize(13);
                            col.Item().PaddingTop(4).Table(tabla =>
                            {
                                tabla.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(6);
                                    cols.ConstantColumn(60);
                                    cols.ConstantColumn(90);
                                });
                                tabla.Header(head =>
                                {
                                    head.Cell().Text("Producto").Bold();
                                    head.Cell().AlignRight().Text("Pares").Bold();
                                    head.Cell().AlignRight().Text("Ingreso").Bold();
                                });
                                foreach (var p in r.TopProductos.Take(15))
                                {
                                    tabla.Cell().PaddingVertical(2).Text(p.Nombre);
                                    tabla.Cell().PaddingVertical(2).AlignRight().Text(p.Cantidad.ToString());
                                    tabla.Cell().PaddingVertical(2).AlignRight().Text(p.Importe.ToString("C2"));
                                }
                            });
                        }

                        // Stock bajo
                        if (r.StockBajo.Count > 0)
                        {
                            col.Item().PaddingVertical(6).LineHorizontal(0.6f);
                            col.Item().Text("Stock bajo (reabastecer)").Bold().FontSize(13);
                            col.Item().PaddingTop(4).Table(tabla =>
                            {
                                tabla.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(6);
                                    cols.ConstantColumn(60);
                                    cols.ConstantColumn(70);
                                });
                                tabla.Header(head =>
                                {
                                    head.Cell().Text("Producto").Bold();
                                    head.Cell().AlignRight().Text("Stock").Bold();
                                    head.Cell().AlignRight().Text("Reorden").Bold();
                                });
                                foreach (var p in r.StockBajo)
                                {
                                    tabla.Cell().PaddingVertical(2).Text(p.Nombre);
                                    tabla.Cell().PaddingVertical(2).AlignRight().Text(p.StockTotal.ToString());
                                    tabla.Cell().PaddingVertical(2).AlignRight().Text(p.PuntoReorden.ToString());
                                }
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Zarely POS  ·  Reporte generado el ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        t.Span($"{DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
            }).GeneratePdf(ruta);

            return ruta;
        }

        private static void Fila(ColumnDescriptor col, string etiqueta, string valor, bool destacado = false)
        {
            col.Item().Row(r =>
            {
                var lbl = r.RelativeItem().Text(etiqueta);
                var val = r.ConstantItem(160).AlignRight().Text(valor);
                if (destacado)
                {
                    lbl.Bold().FontSize(13);
                    val.Bold().FontSize(13);
                }
            });
        }
    }
}
