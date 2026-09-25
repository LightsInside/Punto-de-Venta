using System.ComponentModel.DataAnnotations;

namespace ZarelyPOS.Models
{
    /// <summary>
    /// Proveedor de zapatos. NOTA: si ya tienes una clase Proveedor en tu proyecto,
    /// IGNORA este archivo y conserva el tuyo. El campo importante que debe existir
    /// es 'Nombre' para que el ComboBox del formulario funcione.
    /// </summary>
    public class Proveedor
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(30)]
        public string Telefono { get; set; } = string.Empty;

        [MaxLength(120)]
        public string Correo { get; set; } = string.Empty;

        [MaxLength(250)]
        public string Direccion { get; set; } = string.Empty;
    }
}
