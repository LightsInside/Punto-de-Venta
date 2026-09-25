using System.ComponentModel.DataAnnotations;

namespace ZarelyPOS.Models
{
    /// <summary>
    /// Categoria de zapatos (Tenis, Botas, Sandalias, Formal, etc.).
    /// Catalogo simple administrado por el sistema.
    /// </summary>
    public class Categoria
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(60)]
        public string Nombre { get; set; } = string.Empty;
    }
}
