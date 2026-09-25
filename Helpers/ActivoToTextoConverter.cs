using System.Globalization;
using System.Windows.Data;

namespace ZarelyPOS.Helpers
{
    /// <summary>
    /// Convierte un bool en el texto "Activo" / "Inactivo" para mostrar
    /// en el DataGrid de usuarios de forma mas legible.
    /// </summary>
    [ValueConversion(typeof(bool), typeof(string))]
    public class ActivoToTextoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? "Activo" : "Inactivo";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value as string == "Activo";
    }
}
