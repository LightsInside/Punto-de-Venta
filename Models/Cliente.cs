using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZarelyPOS.Models
{
    /// <summary>
    /// Cliente de la zapateria. Incluye datos de contacto, facturacion y notas.
    /// Para preservar el historial de ventas, los clientes no se eliminan fisicamente:
    /// se desactivan con el campo Activo.
    /// </summary>
    public class Cliente
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Apellido { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Telefono { get; set; } = string.Empty;

        [MaxLength(120)]
        public string Correo { get; set; } = string.Empty;

        /// <summary>
        /// RFC mexicano. Si se captura, debe ser unico en el sistema.
        /// Es opcional porque no todos los clientes piden factura.
        /// </summary>
        [MaxLength(13)]
        public string? RFC { get; set; }

        [MaxLength(250)]
        public string Direccion { get; set; } = string.Empty;

        public DateTime? FechaNacimiento { get; set; }

        [MaxLength(500)]
        public string Notas { get; set; } = string.Empty;

        /// <summary>
        /// Si esta en false, el cliente no aparece en busquedas de ventas nuevas
        /// pero se conserva en el historial de ventas previas.
        /// </summary>
        public bool Activo { get; set; } = true;

        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        // ─── Propiedades calculadas (no van a la BD) ───

        [NotMapped]
        public string NombreCompleto => $"{Nombre} {Apellido}".Trim();

        [NotMapped]
        public string EstadoTexto => Activo ? "Activo" : "Inactivo";
    }
}
