using Microsoft.EntityFrameworkCore;
using ZarelyPOS.Models;

namespace ZarelyPOS.Services
{
    public class DatabaseService
    {
        private readonly ZarelyDbContext _context;

        public DatabaseService()
        {
            _context = new ZarelyDbContext();
            _context.Database.EnsureCreated();
        }

        // ═════════════════════════════════════════════
        //  USUARIOS
        // ═════════════════════════════════════════════

        public async Task<Usuario?> ValidarUsuarioAsync(string nombreUsuario, string password)
            => await _context.Usuarios.FirstOrDefaultAsync(u =>
                u.NombreUsuario == nombreUsuario && u.Password == password && u.Activo);

        public async Task<List<Usuario>> GetUsuariosAsync()
            => await _context.Usuarios.OrderBy(u => u.PrimerNombre).ToListAsync();

        public async Task<Usuario?> GetUsuarioByIdAsync(int id)
            => await _context.Usuarios.FindAsync(id);

        public async Task<bool> ExisteNombreUsuarioAsync(string nombreUsuario, int? idAExcluir = null)
            => await _context.Usuarios.AnyAsync(u =>
                u.NombreUsuario == nombreUsuario && (idAExcluir == null || u.Id != idAExcluir));

        public async Task<Usuario> AddUsuarioAsync(Usuario usuario)
        {
            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();
            return usuario;
        }

        public async Task<bool> UpdateUsuarioAsync(Usuario usuario)
        {
            var existente = await _context.Usuarios.FindAsync(usuario.Id);
            if (existente == null) return false;
            existente.PrimerNombre   = usuario.PrimerNombre;
            existente.PrimerApellido = usuario.PrimerApellido;
            existente.NombreUsuario  = usuario.NombreUsuario;
            existente.Rol            = usuario.Rol;
            existente.Activo         = usuario.Activo;
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>Cuantas ventas registro este usuario (para avisar antes de borrar).</summary>
        public async Task<int> ContarVentasDeUsuarioAsync(int usuarioId)
            => await _context.Ventas.CountAsync(v => v.UsuarioId == usuarioId);

        /// <summary>
        /// Elimina un usuario de forma permanente. Devuelve (false, motivo) si la operacion
        /// dejaria al sistema sin ningun administrador activo, para no bloquear el acceso.
        /// Las ventas conservan su UsuarioNombre (snapshot), asi que el historial no se pierde.
        /// </summary>
        public async Task<(bool Ok, string Mensaje)> DeleteUsuarioAsync(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null) return (false, "El usuario ya no existe.");

            // Nunca dejar el sistema sin administrador: nadie podria volver a entrar.
            if (usuario.Rol == "Administrador" && usuario.Activo)
            {
                var otrosAdmins = await _context.Usuarios
                    .CountAsync(u => u.Rol == "Administrador" && u.Activo && u.Id != id);

                if (otrosAdmins == 0)
                    return (false, "No se puede eliminar el unico administrador activo del sistema.");
            }

            _context.Usuarios.Remove(usuario);
            await _context.SaveChangesAsync();
            return (true, "Usuario eliminado correctamente.");
        }
        public async Task<bool> RecepcionarInventarioAsync(
          int productoId, int? proveedorId, IEnumerable<(string Talla, int Cantidad)> entradas)
        {
            var producto = await _context.Productos
                .Include(p => p.Tallas)
                .FirstOrDefaultAsync(p => p.Id == productoId);

            if (producto == null) return false;

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                if (proveedorId.HasValue)
                    producto.ProveedorId = proveedorId;

                foreach (var (talla, cantidad) in entradas)
                {
                    if (cantidad <= 0 || string.IsNullOrWhiteSpace(talla)) continue;

                    var nombreTalla = talla.Trim();
                    var existente = producto.Tallas
                        .FirstOrDefault(t => t.Talla.Equals(nombreTalla, StringComparison.OrdinalIgnoreCase));

                    if (existente != null)
                        existente.StockActual += cantidad;          // suma al stock actual
                    else
                        producto.Tallas.Add(new TallaInventario     // talla nueva para este producto
                        {
                            ProductoId = producto.Id,
                            Talla = nombreTalla,
                            StockActual = cantidad
                        });
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return true;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        public async Task<bool> CambiarPasswordAsync(int usuarioId, string nuevaPassword)
        {
            var usuario = await _context.Usuarios.FindAsync(usuarioId);
            if (usuario == null) return false;
            usuario.Password = nuevaPassword;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> VerificarPasswordAsync(int usuarioId, string passwordActual)
        {
            var usuario = await _context.Usuarios.FindAsync(usuarioId);
            return usuario != null && usuario.Password == passwordActual;
        }

        // ═════════════════════════════════════════════
        //  CATALOGOS (Categorias / Proveedores / Marcas)
        // ═════════════════════════════════════════════

        public async Task<List<Categoria>> GetCategoriasAsync()
            => await _context.Categorias.OrderBy(c => c.Nombre).ToListAsync();

        public async Task<List<Proveedor>> GetProveedoresAsync()
            => await _context.Proveedores.OrderBy(p => p.Nombre).ToListAsync();

        public async Task<List<string>> GetMarcasAsync()
            => await _context.Productos
                .Where(p => !string.IsNullOrEmpty(p.Marca))
                .Select(p => p.Marca)
                .Distinct()
                .OrderBy(m => m)
                .ToListAsync();

        // ─── NUEVO: CRUD de Proveedores ───

        public async Task<Proveedor?> GetProveedorByIdAsync(int id)
            => await _context.Proveedores.FindAsync(id);

        /// <summary>
        /// Verifica si ya existe otro proveedor con el mismo nombre (case-insensitive).
        /// </summary>
        public async Task<bool> ExisteNombreProveedorAsync(string nombre, int? idAExcluir = null)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return false;
            var nombreNorm = nombre.Trim().ToLower();
            return await _context.Proveedores.AnyAsync(p =>
                p.Nombre.ToLower() == nombreNorm
                && (idAExcluir == null || p.Id != idAExcluir));
        }

        public async Task<Proveedor> AddProveedorAsync(Proveedor proveedor)
        {
            _context.Proveedores.Add(proveedor);
            await _context.SaveChangesAsync();
            return proveedor;
        }

        public async Task<bool> UpdateProveedorAsync(Proveedor proveedor)
        {
            var existente = await _context.Proveedores.FindAsync(proveedor.Id);
            if (existente == null) return false;
            existente.Nombre    = proveedor.Nombre;
            existente.Telefono  = proveedor.Telefono;
            existente.Correo    = proveedor.Correo;
            existente.Direccion = proveedor.Direccion;
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Cuenta cuantos productos tienen asignado este proveedor. Sirve para
        /// advertir antes de eliminar (esos productos quedarian sin proveedor).
        /// </summary>
        public async Task<int> ContarProductosDeProveedorAsync(int proveedorId)
            => await _context.Productos.CountAsync(p => p.ProveedorId == proveedorId);

        public async Task<bool> DeleteProveedorAsync(int id)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);
            if (proveedor == null) return false;
            // Producto.ProveedorId esta configurado como SetNull, asi que los productos
            // de este proveedor no se borran: solo quedan sin proveedor asignado.
            _context.Proveedores.Remove(proveedor);
            await _context.SaveChangesAsync();
            return true;
        }

        // ═════════════════════════════════════════════
        //  PRODUCTOS
        // ═════════════════════════════════════════════

        public async Task<List<Producto>> GetProductosAsync()
            => await _context.Productos
                .Include(p => p.Tallas)
                .Include(p => p.Categoria)
                .Include(p => p.Proveedor)
                .OrderBy(p => p.Marca)
                .ThenBy(p => p.Nombre)
                .ToListAsync();

        public async Task<Producto?> GetProductoByIdAsync(int id)
            => await _context.Productos
                .Include(p => p.Tallas)
                .Include(p => p.Categoria)
                .Include(p => p.Proveedor)
                .FirstOrDefaultAsync(p => p.Id == id);

        public async Task<Producto> AddProductoAsync(Producto producto)
        {
            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();
            return producto;
        }

        public async Task<bool> UpdateProductoAsync(Producto producto, IEnumerable<TallaInventario> tallasNuevas)
        {
            var existente = await _context.Productos
                .Include(p => p.Tallas)
                .FirstOrDefaultAsync(p => p.Id == producto.Id);

            if (existente == null) return false;

            existente.Nombre        = producto.Nombre;
            existente.Marca         = producto.Marca;
            existente.Modelo        = producto.Modelo;
            existente.Color         = producto.Color;
            existente.Precio        = producto.Precio;
            existente.CostoUnitario = producto.CostoUnitario;
            existente.PuntoReorden  = producto.PuntoReorden;
            existente.Estado        = producto.Estado;
            existente.CategoriaId   = producto.CategoriaId;
            existente.ProveedorId   = producto.ProveedorId;
            existente.RutaImagen    = producto.RutaImagen;

            var tallasNuevasList = tallasNuevas.ToList();

            var aEliminar = existente.Tallas
                .Where(t => !tallasNuevasList.Any(n => n.Talla == t.Talla))
                .ToList();
            foreach (var t in aEliminar)
                _context.TallasInventario.Remove(t);

            foreach (var nueva in tallasNuevasList)
            {
                var actual = existente.Tallas.FirstOrDefault(t => t.Talla == nueva.Talla);
                if (actual != null)
                {
                    actual.StockActual = nueva.StockActual;
                }
                else
                {
                    existente.Tallas.Add(new TallaInventario
                    {
                        ProductoId = existente.Id,
                        Talla = nueva.Talla,
                        StockActual = nueva.StockActual
                    });
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteProductoAsync(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return false;
            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync();
            return true;
        }

        // ═════════════════════════════════════════════
        //  CLIENTES
        // ═════════════════════════════════════════════

        public async Task<List<Cliente>> GetClientesAsync()
            => await _context.Clientes
                .OrderBy(c => c.Nombre)
                .ThenBy(c => c.Apellido)
                .ToListAsync();

        public async Task<Cliente?> GetClienteByIdAsync(int id)
            => await _context.Clientes.FindAsync(id);

        /// <summary>
        /// Verifica si ya existe otro cliente con el mismo RFC.
        /// Si el RFC es null o vacio, no se valida (varios clientes pueden no tener RFC).
        /// </summary>
        public async Task<bool> ExisteRFCAsync(string rfc, int? idAExcluir = null)
        {
            if (string.IsNullOrWhiteSpace(rfc)) return false;
            var rfcNormalizado = rfc.Trim().ToUpperInvariant();
            return await _context.Clientes.AnyAsync(c =>
                c.RFC != null
                && c.RFC.ToUpper() == rfcNormalizado
                && (idAExcluir == null || c.Id != idAExcluir));
        }

        public async Task<Cliente> AddClienteAsync(Cliente cliente)
        {
            // Normalizar RFC a mayusculas si tiene valor
            if (!string.IsNullOrWhiteSpace(cliente.RFC))
                cliente.RFC = cliente.RFC.Trim().ToUpperInvariant();
            else
                cliente.RFC = null;

            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();
            return cliente;
        }

        /// <summary>Cuantas ventas tiene registradas este cliente (para avisar antes de borrar).</summary>
        public async Task<int> ContarVentasDeClienteAsync(int clienteId)
            => await _context.Ventas.CountAsync(v => v.ClienteId == clienteId);

        /// <summary>
        /// Elimina un cliente de forma permanente. Venta.ClienteId esta configurado como
        /// SetNull, asi que las ventas del cliente NO se borran: conservan su ClienteNombre
        /// (snapshot) y solo pierden la referencia.
        /// </summary>
        public async Task<bool> DeleteClienteAsync(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null) return false;

            _context.Clientes.Remove(cliente);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateClienteAsync(Cliente cliente)
        {
            var existente = await _context.Clientes.FindAsync(cliente.Id);
            if (existente == null) return false;

            existente.Nombre          = cliente.Nombre;
            existente.Apellido        = cliente.Apellido;
            existente.Telefono        = cliente.Telefono;
            existente.Correo          = cliente.Correo;
            existente.RFC             = string.IsNullOrWhiteSpace(cliente.RFC)
                                          ? null
                                          : cliente.RFC.Trim().ToUpperInvariant();
            existente.Direccion       = cliente.Direccion;
            existente.FechaNacimiento = cliente.FechaNacimiento;
            existente.Notas           = cliente.Notas;
            existente.Activo          = cliente.Activo;

            await _context.SaveChangesAsync();
            return true;
        }

        // ═════════════════════════════════════════════
        //  VENTAS  (NUEVO)
        // ═════════════════════════════════════════════

        public async Task<List<Venta>> GetVentasAsync()
            => await _context.Ventas
                .Include(v => v.Detalles)
                .OrderByDescending(v => v.Fecha)
                .ToListAsync();

        public async Task<Venta?> GetVentaByIdAsync(int id)
            => await _context.Ventas
                .Include(v => v.Detalles)
                .FirstOrDefaultAsync(v => v.Id == id);

        /// <summary>
        /// Devuelve las ventas de un dia concreto (de 00:00 a 23:59:59), con sus
        /// detalles incluidos. Se usa para el corte de caja y el filtro "ventas del dia".
        /// Se filtra por rango [dia, dia+1) porque es lo mas seguro/portable en SQLite.
        /// </summary>
        public async Task<List<Venta>> GetVentasPorFechaAsync(DateTime dia)
        {
            var desde = dia.Date;
            var hasta = desde.AddDays(1);

            return await _context.Ventas
                .Include(v => v.Detalles)
                .Where(v => v.Fecha >= desde && v.Fecha < hasta)
                .OrderBy(v => v.Fecha)
                .ToListAsync();
        }

        /// <summary>
        /// Registra una venta de forma atomica (todo o nada):
        ///   1. Re-valida el stock de cada talla AL MOMENTO de cobrar (no en el momento
        ///      en que se agrego al carrito), para evitar vender mas de lo que hay si
        ///      otra venta ocurrio en paralelo.
        ///   2. Descuenta el stock de cada TallaInventario.
        ///   3. Guarda la venta con sus detalles.
        /// Si algo falla, se revierte TODO con la transaccion (rollback).
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Si una talla ya no existe o no hay stock suficiente.
        /// </exception>
        public async Task<Venta> RegistrarVentaAsync(Venta venta)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var detalle in venta.Detalles)
                {
                    if (detalle.TallaInventarioId is int tallaId)
                    {
                        var talla = await _context.TallasInventario
                            .FirstOrDefaultAsync(t => t.Id == tallaId);

                        if (talla == null)
                            throw new InvalidOperationException(
                                $"La talla del producto '{detalle.ProductoNombre}' ya no existe.");

                        if (talla.StockActual < detalle.Cantidad)
                            throw new InvalidOperationException(
                                $"Stock insuficiente para '{detalle.ProductoNombre}' (talla {detalle.Talla}). " +
                                $"Disponible: {talla.StockActual}, solicitado: {detalle.Cantidad}.");

                        talla.StockActual -= detalle.Cantidad;
                    }
                }

                _context.Ventas.Add(venta);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return venta;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Elimina una venta de forma atomica (todo o nada). Si la venta seguia en
        /// estado "Completado", DEVUELVE el stock de cada renglon a su talla
        /// correspondiente (deshace el descuento que hizo RegistrarVentaAsync):
        ///   1. Carga la venta con sus detalles.
        ///   2. Si Estado == "Completado", reintegra el stock de cada TallaInventario
        ///      que todavia exista (si la talla fue borrada, se omite sin fallar).
        ///   3. Borra la venta; sus detalles se eliminan en cascada.
        /// Si algo falla, se revierte TODO con la transaccion (rollback).
        /// </summary>
        /// <returns>true si se elimino; false si la venta ya no existia.</returns>
        public async Task<bool> EliminarVentaAsync(int ventaId)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var venta = await _context.Ventas
                    .Include(v => v.Detalles)
                    .FirstOrDefaultAsync(v => v.Id == ventaId);

                if (venta == null)
                    return false;

                // Solo reintegramos stock si la venta seguia activa. Asi evitamos
                // devolver dos veces el stock de una venta ya cancelada.
                if (venta.Estado == "Completado")
                {
                    foreach (var detalle in venta.Detalles)
                    {
                        if (detalle.TallaInventarioId is int tallaId)
                        {
                            var talla = await _context.TallasInventario
                                .FirstOrDefaultAsync(t => t.Id == tallaId);

                            // Si la talla ya no existe, no hay donde reintegrar: se omite.
                            if (talla != null)
                                talla.StockActual += detalle.Cantidad;
                        }
                    }
                }

                _context.Ventas.Remove(venta); // los detalles caen en cascada
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return true;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
    }
}
