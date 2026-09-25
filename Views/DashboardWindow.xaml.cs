using System.Windows;
using System.Windows.Controls;
using ZarelyPOS.Helpers;

namespace ZarelyPOS.Views
{
    public partial class DashboardWindow : Window
    {
        public DashboardWindow()
        {
            InitializeComponent();
            CargarDatos();
        }

        private void CargarDatos()
        {
            var usuario = SessionManager.UsuarioActual!;
            TxtUsuario.Text = $"{usuario.PrimerNombre} {usuario.PrimerApellido} ({usuario.Rol})";
            TxtBienvenida.Text = $"Bienvenido, {usuario.PrimerNombre} {usuario.PrimerApellido}";

            // Mensaje distinto segun el rol para que sea explicito que opciones tiene
            TxtSubtitulo.Text = SessionManager.EsAdmin
                ? "Tienes acceso a todos los modulos del sistema."
                : "Tienes acceso a Ventas, Productos (solo consulta), Clientes y Reportes.";

            // ╔═══════════════════════════════════════════════════════════╗
            // ║  PERMISOS POR MODULO                                      ║
            // ║                                                           ║
            // ║  SoloAdmin = true  → SOLO el administrador lo ve.         ║
            // ║  SoloAdmin = false → Todos los usuarios lo ven.           ║
            // ║                                                           ║
            // ║  Para usuarios regulares:                                 ║
            // ║   - Ventas, Productos, Clientes, Reportes → SI tienen     ║
            // ║   - Proveedores, Recepcion, Usuarios     → NO tienen      ║
            // ║                                                           ║
            // ║  Dentro de cada modulo, los permisos finos (por ejemplo  ║
            // ║  "no puede agregar productos") se controlan en su propia ║
            // ║  ventana usando SessionManager.EsAdmin.                  ║
            // ╚═══════════════════════════════════════════════════════════╝
            var modulos = new List<ModuloItem>
            {
                new("Ventas",                "Registrar nuevas ventas",                "🛒", "ventas",     false),
                new("Historial de Ventas",   "Consultar y administrar ventas",         "🧾", "historial-ventas", false),
                new("Productos",             "Consultar inventario",                   "📦", "productos",  false),
                new("Recepcion de Mercancia","Registrar entradas de inventario","📥", "recepcion",  false),
                new("Clientes",              "Administrar clientes",                   "👥", "clientes",   false),
                new("Reportes",              "Ver reportes y estadisticas",            "📊", "reportes",   false),
                new("Corte de Caja",         "Resumen del dia y exportar a PDF",       "💰", "corte-caja", false),
                new("Proveedores",           "Gestionar proveedores",                  "🚚", "proveedores",true),
                new("Gestion de Usuarios",   "Administrar usuarios y accesos",         "🔑", "usuarios",   true),
            };

            // Filtrar segun rol
            ListModulos.ItemsSource = modulos
                .Where(m => !m.SoloAdmin || SessionManager.EsAdmin)
                .ToList();
        }

        private void ModuloBtn_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            var ruta = btn.Tag?.ToString();

            // Defensa extra: aunque el modulo este oculto en el dashboard,
            // verificamos aqui que el usuario regular no pueda entrar a un
            // modulo de solo-admin por algun camino inesperado.
            if (!SessionManager.EsAdmin && EsModuloSoloAdmin(ruta))
            {
                MessageBox.Show(
                    "No tienes permisos para acceder a este modulo.",
                    "Acceso denegado",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Window? ventana = ruta switch
            {
                "productos" => new ProductosWindow(),
                "recepcion" => new RecepcionWindow(),
                "usuarios"  => new UsuariosWindow(),
                "clientes" => new ClientesWindow(),
                "ventas"   => new VentasWindow(),
                "historial-ventas" => new HistorialVentasWindow(),
                "corte-caja" => new CorteCajaWindow(),
                "reportes" => new ReportesWindow(),
                "proveedores" => new ProveedoresWindow(),
                // Aqui se agregan las demas ventanas conforme se implementen
                _ => null
            };

            if (ventana != null)
            {
                ventana.Owner = this;
                ventana.ShowDialog();
            }
            else
            {
                MessageBox.Show("Este modulo aun no esta implementado.", "En desarrollo",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private static bool EsModuloSoloAdmin(string? ruta) =>
            ruta is "proveedores" or "usuarios";

        private async void BtnCambiarPassword_Click(object sender, RoutedEventArgs e)
        {
            var usuarioActual = SessionManager.UsuarioActual;
            if (usuarioActual == null) return;

            var dialogo = new CambiarPasswordWindow(usuarioActual, modoAdmin: false)
            {
                Owner = this
            };

            if (dialogo.ShowDialog() == true && !string.IsNullOrEmpty(dialogo.NuevaPassword))
            {
                await App.Db.CambiarPasswordAsync(usuarioActual.Id, dialogo.NuevaPassword);
                MessageBox.Show(
                    "Tu contrasena ha sido actualizada correctamente.",
                    "Zarely POS",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Cerrar la sesion actual y abrir el login para que entre otro usuario.
        /// Funciona igual que "Cerrar sesion" — la diferencia es solo el nombre
        /// del boton, que comunica mas claramente la intencion al usuario.
        /// </summary>
        private void BtnCambiarCuenta_Click(object sender, RoutedEventArgs e)
        {
            var nombre = SessionManager.UsuarioActual?.PrimerNombre ?? "usuario actual";
            var resultado = MessageBox.Show(
                $"Se cerrara la sesion de {nombre} y se mostrara la pantalla de inicio de sesion para que otro usuario pueda entrar.\n\n¿Continuar?",
                "Cambiar de cuenta",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (resultado != MessageBoxResult.Yes) return;

            SessionManager.CerrarSesion();
            new LoginWindow().Show();
            this.Close();
        }

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            SessionManager.CerrarSesion();
            new LoginWindow().Show();
            this.Close();
        }
    }

    public record ModuloItem(string Titulo, string Descripcion, string Icono, string Ruta, bool SoloAdmin);
}
