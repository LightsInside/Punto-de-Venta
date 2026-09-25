using System.Windows;
using ZarelyPOS.Services;

namespace ZarelyPOS
{
    public partial class App : Application
    {
        // Instancia única del servicio de base de datos (singleton simple)
        public static DatabaseService Db { get; } = new DatabaseService();
    }
}
