using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZarelyPOS.Models
{
    /// <summary>
    /// Modelo de zapato (ej. "Zoom Negra Nike"). Agrupa varias tallas en TallaInventario.
    /// Precio y costo son por modelo (no varian por talla).
    /// </summary>
    public class Producto
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(80)]
        public string Marca { get; set; } = string.Empty;

        [MaxLength(80)]
        public string Modelo { get; set; } = string.Empty;

        [MaxLength(40)]
        public string Color { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Precio { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CostoUnitario { get; set; }

        public int? PuntoReorden { get; set; }

        [Required, MaxLength(20)]
        public string Estado { get; set; } = "Activo"; // "Activo" | "Inactivo"

        // ─── Relaciones ───
        public int? CategoriaId { get; set; }

        [ForeignKey(nameof(CategoriaId))]
        public Categoria? Categoria { get; set; }

        public int? ProveedorId { get; set; }

        [ForeignKey(nameof(ProveedorId))]
        public Proveedor? Proveedor { get; set; }

        /// <summary>
        /// Ruta relativa a la imagen del producto (dentro de /Images del ejecutable).
        /// Null si el producto no tiene imagen.
        /// </summary>
        [MaxLength(260)]
        public string? RutaImagen { get; set; }

        // ─── Coleccion de tallas (variantes con stock) ───
        public List<TallaInventario> Tallas { get; set; } = new();

        // ─── Propiedades calculadas (no van a la BD) ───

        [NotMapped]
        public int StockTotal => Tallas?.Sum(t => t.StockActual) ?? 0;

        [NotMapped]
        public string ResumenTallas => Tallas is { Count: > 0 }
            ? string.Join("   ", Tallas
                .OrderBy(t => t.Talla)
                .Select(t => $"{t.Talla}:{t.StockActual}"))
            : "(sin tallas)";

        [NotMapped]
        public bool StockBajo => PuntoReorden.HasValue && StockTotal <= PuntoReorden.Value;
    }
}
