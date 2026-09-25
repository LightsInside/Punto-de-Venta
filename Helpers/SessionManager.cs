using ZarelyPOS.Models;

namespace ZarelyPOS.Helpers
{
    /// <summary>
    /// Equivalente al localStorage del proyecto React.
    /// Guarda en memoria la sesión del usuario autenticado.
    /// </summary>
    public static class SessionManager
    {
        public static Usuario? UsuarioActual { get; private set; }

        public static bool EsAdmin => UsuarioActual?.Rol == "Administrador";

        public static void IniciarSesion(Usuario usuario)
        {
            UsuarioActual = usuario;
        }

        public static void CerrarSesion()
        {
            UsuarioActual = null;
        }

        public static string NombreDisplay =>
            UsuarioActual != null
                ? $"{UsuarioActual.PrimerNombre} {UsuarioActual.PrimerApellido} — {UsuarioActual.Rol}"
                : "Sin sesión";
    }
}
