using System.IO;
using System.Windows.Media.Imaging;

namespace ZarelyPOS.Helpers
{
    /// <summary>
    /// Helper para manejar imagenes de productos: guardarlas en una carpeta local
    /// y cargarlas como BitmapImage para mostrarlas en WPF sin bloquear el archivo.
    /// </summary>
    public static class ImagenHelper
    {
        /// <summary>
        /// Carpeta donde se guardan las imagenes (junto al ejecutable):
        ///   .../bin/Debug/net8.0-windows/Images/
        /// </summary>
        public static string CarpetaImagenes
        {
            get
            {
                var ruta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
                Directory.CreateDirectory(ruta);
                return ruta;
            }
        }

        /// <summary>
        /// Copia un archivo de imagen seleccionado por el usuario hacia la carpeta de imagenes
        /// con un nombre unico, y devuelve la ruta relativa (que se guarda en BD).
        /// </summary>
        public static string GuardarImagen(string archivoOrigen)
        {
            if (string.IsNullOrWhiteSpace(archivoOrigen) || !File.Exists(archivoOrigen))
                throw new FileNotFoundException("La imagen seleccionada no existe.", archivoOrigen);

            var extension = Path.GetExtension(archivoOrigen).ToLowerInvariant();
            var nombreUnico = $"prod_{Guid.NewGuid():N}{extension}";
            var destino = Path.Combine(CarpetaImagenes, nombreUnico);

            File.Copy(archivoOrigen, destino, overwrite: false);

            // Guardamos la ruta RELATIVA en la BD para que la app sea portable
            return Path.Combine("Images", nombreUnico);
        }

        /// <summary>
        /// Borra una imagen vieja cuando se reemplaza por una nueva (ignora errores).
        /// </summary>
        public static void BorrarImagen(string? rutaRelativa)
        {
            if (string.IsNullOrWhiteSpace(rutaRelativa)) return;
            try
            {
                var ruta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, rutaRelativa);
                if (File.Exists(ruta)) File.Delete(ruta);
            }
            catch { /* si falla el borrado no es critico */ }
        }

        /// <summary>
        /// Carga una imagen desde disco como BitmapImage. Usa CacheOption.OnLoad
        /// para que el archivo NO quede bloqueado (importante: si no se hace asi,
        /// no se puede sobreescribir/borrar despues).
        /// Devuelve null si la ruta es invalida o el archivo no existe.
        /// </summary>
        public static BitmapImage? CargarBitmap(string? rutaRelativa)
        {
            if (string.IsNullOrWhiteSpace(rutaRelativa)) return null;

            var rutaCompleta = Path.IsPathRooted(rutaRelativa)
                ? rutaRelativa
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, rutaRelativa);

            if (!File.Exists(rutaCompleta)) return null;

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bmp.UriSource = new Uri(rutaCompleta, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze(); // permite usarla desde cualquier hilo
                return bmp;
            }
            catch
            {
                return null;
            }
        }
    }
}
