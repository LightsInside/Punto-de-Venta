using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZarelyPOS.Models
{
    /// <summary>
    /// Renglon de una venta: un producto en una talla especifica, con su cantidad.
    /// Guarda un snapshot (nombre, talla y precio) para que el ticket historico no
    /// cambie aunque despues se edite o elimine el producto. Por eso ProductoId y
    /// TallaInventarioId son ints "sueltos" (sin relacion configurada): la venta
    /// sobrevive aunque el producto desaparezca.
    /// </summary>
    public class DetalleVenta
    {
        [Key]
        public int Id { get; set; }

        public int VentaId { get; set; }

        [ForeignKey(nameof(VentaId))]
        public Venta? Venta { get; set; }

        /// <summary>Referencia al producto vendido (solo informativa, sin FK dura).</summary>
        public int ProductoId { get; set; }

        /// <summary>
        /// Talla/variante exacta de la que se descuenta el stock. Puede ser null si
        /// el producto se vendio sin manejar tallas.
        /// </summary>
        public int? TallaInventarioId { get; set; }

        // ─── Snapshots al momento de la venta ───
        [Required, MaxLength(150)]
        public string ProductoNombre { get; set; } = string.Empty;

        [MaxLength(10)]
        public string Talla { get; set; } = string.Empty;

        public int Cantidad { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioUnitario { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }
    }
}
