using System.Globalization;
using System.Windows.Data;
using ZarelyPOS.Helpers;

namespace ZarelyPOS.Helpers
{
    /// <summary>
    /// Convierte una ruta relativa de imagen en un BitmapImage utilizable por el control Image.
    /// Si la ruta es null/invalida, devuelve null (la imagen aparecera vacia).
    ///
    /// Uso en XAML:
    ///   &lt;Image Source="{Binding RutaImagen, Converter={StaticResource RutaImagenConverter}}" /&gt;
    /// </summary>
    [ValueConversion(typeof(string), typeof(System.Windows.Media.Imaging.BitmapImage))]
    public class RutaImagenConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => ImagenHelper.CargarBitmap(value as string);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
