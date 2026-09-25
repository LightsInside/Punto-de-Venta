using Microsoft.EntityFrameworkCore;
using ZarelyPOS.Models;
using System.IO;

namespace ZarelyPOS.Services
{
    public class ZarelyDbContext : DbContext
    {
        public DbSet<Producto> Productos { get; set; }
        public DbSet<TallaInventario> TallasInventario { get; set; }
        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<Proveedor> Proveedores { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Cliente> Clientes { get; set; }

        // ─── NUEVO: Ventas ───
        public DbSet<Venta> Ventas { get; set; }
        public DbSet<DetalleVenta> DetallesVenta { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zarely.db");
            options.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ─── Usuarios ───
            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.NombreUsuario)
                .IsUnique();

            // ─── Producto ↔ TallaInventario ───
            modelBuilder.Entity<TallaInventario>()
                .HasOne(t => t.Producto)
                .WithMany(p => p.Tallas)
                .HasForeignKey(t => t.ProductoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TallaInventario>()
                .HasIndex(t => new { t.ProductoId, t.Talla })
                .IsUnique();

            // ─── Producto ↔ Categoria / Proveedor ───
            modelBuilder.Entity<Producto>()
                .HasOne(p => p.Categoria)
                .WithMany()
                .HasForeignKey(p => p.CategoriaId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Producto>()
                .HasOne(p => p.Proveedor)
                .WithMany()
                .HasForeignKey(p => p.ProveedorId)
                .OnDelete(DeleteBehavior.SetNull);

            // ─── Clientes: indice unico en RFC (cuando no es null) ───
            // SQLite por defecto permite varios NULL en un indice unico, asi que esto
            // funciona correctamente: solo bloquea RFCs duplicados que NO sean null.
            modelBuilder.Entity<Cliente>()
                .HasIndex(c => c.RFC)
                .IsUnique();

            // ─── NUEVO: Venta ↔ DetalleVenta (cascade) ───
            // Si se elimina una venta, sus renglones se borran con ella.
            modelBuilder.Entity<DetalleVenta>()
                .HasOne(d => d.Venta)
                .WithMany(v => v.Detalles)
                .HasForeignKey(d => d.VentaId)
                .OnDelete(DeleteBehavior.Cascade);

            // ─── NUEVO: Venta ↔ Cliente (SetNull) ───
            // Si algun dia se borra un cliente, la venta conserva su ClienteNombre
            // (snapshot) y solo pierde la referencia.
            modelBuilder.Entity<Venta>()
                .HasOne(v => v.Cliente)
                .WithMany()
                .HasForeignKey(v => v.ClienteId)
                .OnDelete(DeleteBehavior.SetNull);

            // ═══════════════════════════════════════
            //  SEED
            // ═══════════════════════════════════════

            // ─── Usuario administrador inicial ───
            // Se conserva UNICAMENTE esta cuenta: sin ella nadie podria iniciar sesion
            // y el sistema quedaria inaccesible.  usuario: admin  ·  contrasena: admin123
            modelBuilder.Entity<Usuario>().HasData(
                new Usuario
                {
                    Id = 1, PrimerNombre = "Ivan", PrimerApellido = "Rubio",
                    NombreUsuario = "admin", Password = "admin123",
                    Rol = "Administrador", Activo = true
                }
            );

            // ─── Categorias (catalogo) ───
            // Se conservan porque el sistema no tiene pantalla para darlas de alta:
            // si se borraran, no habria forma de crearlas desde la aplicacion.
            modelBuilder.Entity<Categoria>().HasData(
                new Categoria { Id = 1, Nombre = "Tenis" },
                new Categoria { Id = 2, Nombre = "Botas" },
                new Categoria { Id = 3, Nombre = "Sandalias" },
                new Categoria { Id = 4, Nombre = "Formal" },
                new Categoria { Id = 5, Nombre = "Casual" }
            );

            // ─── Sin datos de demostracion ───
            // Proveedores, productos, tallas, clientes y ventas arrancan VACIOS
            // para poder demostrar el alta de informacion desde cero.
        }
    }
}
