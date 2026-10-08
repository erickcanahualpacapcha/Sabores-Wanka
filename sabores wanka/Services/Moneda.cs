using System.Globalization;

namespace sabores_wanka.Services
{
    // Formato banco peruano: S/ 1,485.50 (miles con coma, decimales con punto).
    // Un solo punto para que todos los precios se vean igual en la app.
    public static class Moneda
    {
        private static readonly CultureInfo Peru = new("es-PE");

        // Con simbolo: "S/ 1,485.50" (vitrinas, totales, tickets UI).
        public static string Texto(double valor) => "S/ " + Numero(valor);

        // Solo numero: "1,485.50" (campos que ya traen su "S/" aparte).
        public static string Numero(double valor) => valor.ToString("N2", Peru);
    }
}
