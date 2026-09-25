using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZarelyPOS.Models
{
    /// <summary>
    /// Cabecera de una venta (un "ticket"). Agrupa los renglones en DetalleVenta.
    /// Guarda un snapshot del cliente y del usuario que la registro para que el
    /// historial no cambie aunque despues se edite o desactive ese cliente/usuario.
    /// </summary>
    public class Venta
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Cliente asociado. Es null cuando la venta es a "Publico en general".
        /// </summary>
        public int? ClienteId { get; set; }

        [ForeignKey(nameof(ClienteId))]
        public Cliente? Cliente { get; set; }

        /// <summary>Nombre del cliente al momento de la venta (snapshot inmutable).</summary>
        [MaxLength(150)]
        public string ClienteNombre { get; set; } = "Publico en general";

        public DateTime Fecha { get; set; } = DateTime.Now;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        /// <summary>Porcentaje de descuento aplicado a toda la venta (0 a 100).</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal DescuentoPorcentaje { get; set; }

        /// <summary>Monto descontado en dinero (Subtotal * DescuentoPorcentaje / 100).</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal DescuentoMonto { get; set; }

        /// <summary>Total final a pagar = Subtotal - DescuentoMonto.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        // "Efectivo" | "Tarjeta"
        [Required, MaxLength(20)]
        public string MetodoPago { get; set; } = "Efectivo";

        /// <summary>Solo aplica cuando MetodoPago == "Efectivo".</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? DineroRecibido { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Cambio { get; set; }

        // "Completado" | "Cancelado"
        [Required, MaxLength(20)]
        public string Estado { get; set; } = "Completado";

        // ─── Snapshot del usuario que registro la venta ───
        public int? UsuarioId { get; set; }

        [MaxLength(150)]
        public string UsuarioNombre { get; set; } = string.Empty;

        // ─── Renglones de la venta ───
        public List<DetalleVenta> Detalles { get; set; } = new();

        // ─── Propiedades calculadas (no van a la BD) ───

        [NotMapped]
        public int TotalArticulos => Detalles?.Sum(d => d.Cantidad) ?? 0;

        [NotMapped]
        public string FechaTexto => Fecha.ToString("dd/MM/yyyy HH:mm");
    }
}
