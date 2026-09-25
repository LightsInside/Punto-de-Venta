namespace ZarelyPOS.Models
{
    /// <summary>
    /// Resumen de estadisticas para el modulo de Reportes en un rango de fechas.
    /// No es entidad de BD: se calcula a partir de las ventas y productos.
    /// </summary>
    public class ReporteResumen
    {
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }

        public int NumVentas { get; set; }
        public decimal TotalVendido { get; set; }
        public decimal Descuentos { get; set; }

        public int NumEfectivo { get; set; }
        public decimal TotalEfectivo { get; set; }
        public int NumTarjeta { get; set; }
        public decimal TotalTarjeta { get; set; }

        public List<ProductoVendido> TopProductos { get; set; } = new();
        public List<DiaVenta> VentasPorDia { get; set; } = new();
        public List<ProductoStock> StockBajo { get; set; } = new();

        public int UnidadesVendidas => TopProductos.Sum(p => p.Cantidad);
        public decimal TicketPromedio => NumVentas > 0 ? TotalVendido / NumVentas : 0m;

        // Porcentajes por metodo de pago (0 si no hubo ventas).
        public double PctEfectivo => TotalVendido > 0 ? (double)(TotalEfectivo / TotalVendido) * 100 : 0;
        public double PctTarjeta => TotalVendido > 0 ? (double)(TotalTarjeta / TotalVendido) * 100 : 0;

        public static ReporteResumen DesdeDatos(DateTime desde, DateTime hasta,
                                                List<Venta> ventas, List<Producto> productos)
        {
            var d0 = desde.Date;
            var d1 = hasta.Date;

            var rango = ventas
                .Where(v => v.Estado == "Completado" && v.Fecha.Date >= d0 && v.Fecha.Date <= d1)
                .ToList();

            var efectivo = rango.Where(v => v.MetodoPago == "Efectivo").ToList();
            var tarjeta = rango.Where(v => v.MetodoPago == "Tarjeta").ToList();

            // Top productos (agrupados por nombre, sumando todas las tallas)
            var top = rango
                .SelectMany(v => v.Detalles)
                .GroupBy(d => d.ProductoNombre)
                .Select(g => new ProductoVendido
                {
                    Nombre = g.Key,
                    Talla = string.Empty,
                    Cantidad = g.Sum(x => x.Cantidad),
                    Importe = g.Sum(x => x.Subtotal)
                })
                .OrderByDescending(p => p.Cantidad)
                .ThenByDescending(p => p.Importe)
                .ToList();

            // Ventas por dia: un punto por cada dia del rango si es razonable (<= 92 dias);
            // si el rango es enorme, solo los dias que tuvieron ventas.
            List<DiaVenta> porDia;
            var totalDias = (d1 - d0).Days;
            if (totalDias >= 0 && totalDias <= 92)
            {
                porDia = new List<DiaVenta>();
                for (var dia = d0; dia <= d1; dia = dia.AddDays(1))
                    porDia.Add(new DiaVenta { Dia = dia, Total = rango.Where(v => v.Fecha.Date == dia).Sum(v => v.Total) });
            }
            else
            {
                porDia = rango
                    .GroupBy(v => v.Fecha.Date)
                    .Select(g => new DiaVenta { Dia = g.Key, Total = g.Sum(v => v.Total) })
                    .OrderBy(x => x.Dia)
                    .ToList();
            }

            // Stock bajo: activos cuyo stock total esta en o por debajo del punto de reorden.
            var stockBajo = productos
                .Where(p => p.Estado == "Activo" && p.PuntoReorden.HasValue && p.StockTotal <= p.PuntoReorden.Value)
                .OrderBy(p => p.StockTotal)
                .Select(p => new ProductoStock
                {
                    Nombre = $"{p.Marca} {p.Nombre} ({p.Color})",
                    StockTotal = p.StockTotal,
                    PuntoReorden = p.PuntoReorden ?? 0
                })
                .ToList();

            return new ReporteResumen
            {
                Desde = d0,
                Hasta = d1,
                NumVentas = rango.Count,
                TotalVendido = rango.Sum(v => v.Total),
                Descuentos = rango.Sum(v => v.DescuentoMonto),
                NumEfectivo = efectivo.Count,
                TotalEfectivo = efectivo.Sum(v => v.Total),
                NumTarjeta = tarjeta.Count,
                TotalTarjeta = tarjeta.Sum(v => v.Total),
                TopProductos = top,
                VentasPorDia = porDia,
                StockBajo = stockBajo
            };
        }
    }

    /// <summary>Total vendido en un dia (para la grafica de barras).</summary>
    public class DiaVenta
    {
        public DateTime Dia { get; set; }
        public decimal Total { get; set; }
        public string Etiqueta => Dia.ToString("dd/MM");
    }

    /// <summary>Producto activo con stock en o bajo su punto de reorden.</summary>
    public class ProductoStock
    {
        public string Nombre { get; set; } = string.Empty;
        public int StockTotal { get; set; }
        public int PuntoReorden { get; set; }
    }
}
