using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZarelyPOS.Models
{
    /// <summary>
    /// Stock de un producto en una talla especifica.
    /// Ejemplo: Producto "Zoom Negra Nike" tiene una TallaInventario por cada talla
    /// (T38=100, T39=50, T40=20, etc.).
    /// </summary>
    public class TallaInventario
    {
        [Key]
        public int Id { get; set; }

        public int ProductoId { get; set; }

        [ForeignKey(nameof(ProductoId))]
        public Producto? Producto { get; set; }

        /// <summary>
        /// La talla como texto (puede ser "38", "26.5", "XL", etc.).
        /// Permite mas flexibilidad que un numero.
        /// </summary>
        [Required, MaxLength(10)]
        public string Talla { get; set; } = string.Empty;

        public int StockActual { get; set; }
    }
}
