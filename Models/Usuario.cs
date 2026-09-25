using System.ComponentModel.DataAnnotations;

namespace ZarelyPOS.Models
{
    public class Usuario
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string PrimerNombre { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string PrimerApellido { get; set; } = string.Empty;

        /// <summary>
        /// Nombre de usuario para iniciar sesion (no es un correo electronico).
        /// </summary>
        [Required, MaxLength(50)]
        public string NombreUsuario { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Password { get; set; } = string.Empty;

        // "Administrador" | "Usuario Regular"
        [Required, MaxLength(50)]
        public string Rol { get; set; } = "Usuario Regular";

        /// <summary>
        /// Si esta en false el usuario no puede iniciar sesion (bloqueado por el admin).
        /// </summary>
        public bool Activo { get; set; } = true;

        public string NombreCompleto => $"{PrimerNombre} {PrimerApellido}";
    }
}
