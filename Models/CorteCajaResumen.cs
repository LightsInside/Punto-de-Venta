namespace ZarelyPOS.Models
{
    /// <summary>
    /// Resumen (no es entidad de BD) con los totales de un dia para el corte de caja.
    /// Se calcula a partir de las ventas "Completado" de la fecha seleccionada.
    /// </summary>
    public class CorteCajaResumen
    {
        public DateTime Dia { get; set; }

        public int NumVentas { get; set; }
        public decimal SubtotalBruto { get; set; }   // antes de descuentos
        public decimal Descuentos { get; set; }
        public decimal TotalVendido { get; set; }     // ya con descuentos aplicados

        public int NumEfectivo { get; set; }
        public decimal TotalEfectivo { get; set; }

        public int NumTarjeta { get; set; }
        public decimal TotalTarjeta { get; set; }

        public decimal Fondo { get; set; }            // fondo inicial de caja

        /// <summary>Zapatos vendidos en el dia, agrupados por producto y talla.</summary>
        public List<ProductoVendido> Productos { get; set; } = new();

        /// <summary>Total de pares/unidades vendidas en el dia.</summary>
        public int UnidadesVendidas => Productos.Sum(p => p.Cantidad);

        /// <summary>Efectivo que deberia haber fisicamente en la caja al cerrar.</summary>
        public decimal EfectivoEnCaja => Fondo + TotalEfectivo;

        /// <summary>Ticket promedio del dia (0 si no hubo ventas).</summary>
        public decimal TicketPromedio => NumVentas > 0 ? TotalVendido / NumVentas : 0m;

        /// <summary>
        /// Construye el resumen a partir de la lista de ventas del dia.
        /// El SubtotalBruto se deriva de Total + Descuentos para que sea correcto
        /// aunque alguna venta antigua no tenga el campo Subtotal poblado.
        /// </summary>
        public static CorteCajaResumen DesdeVentas(DateTime dia, List<Venta> ventas, decimal fondo)
        {
            var efectivo = ventas.Where(v => v.MetodoPago == "Efectivo").ToList();
            var tarjeta = ventas.Where(v => v.MetodoPago == "Tarjeta").ToList();

            var totalVendido = ventas.Sum(v => v.Total);
            var descuentos = ventas.Sum(v => v.DescuentoMonto);

            return new CorteCajaResumen
            {
                Dia = dia.Date,
                NumVentas = ventas.Count,
                TotalVendido = totalVendido,
                Descuentos = descuentos,
                SubtotalBruto = totalVendido + descuentos,
                NumEfectivo = efectivo.Count,
                TotalEfectivo = efectivo.Sum(v => v.Total),
                NumTarjeta = tarjeta.Count,
                TotalTarjeta = tarjeta.Sum(v => v.Total),
                Fondo = fondo,
                Productos = ventas
                    .SelectMany(v => v.Detalles)
                    .GroupBy(d => new { d.ProductoNombre, d.Talla })
                    .Select(g => new ProductoVendido
                    {
                        Nombre = g.Key.ProductoNombre,
                        Talla = g.Key.Talla,
                        Cantidad = g.Sum(d => d.Cantidad),
                        Importe = g.Sum(d => d.Subtotal)
                    })
                    .OrderByDescending(p => p.Cantidad)
                    .ThenBy(p => p.Nombre)
                    .ToList()
            };
        }
    }

    /// <summary>Un renglon del desglose de zapatos vendidos en el corte.</summary>
    public class ProductoVendido
    {
        public string Nombre { get; set; } = string.Empty;
        public string Talla { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal Importe { get; set; }
    }
}
