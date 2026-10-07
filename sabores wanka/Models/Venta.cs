using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace sabores_wanka.Models
{
    // Ítem vendido dentro de una boleta (copia plana del CartItem del POS).
    public sealed class ItemVenta
    {
        public string Nombre { get; set; } = "";
        public double Precio { get; set; }
        public int Cantidad { get; set; }
    }

    // Venta cerrada: una boleta por cada confirmación de cierre (total o parcial).
    // Solo PAGADO vive en Ingresos; POR COBRAR vive en Por cobrar hasta cobrarse.
    // EN MESA es legado (cuenta abierta) y tambien va a Por cobrar, nunca a Ingresos.
    public sealed class Venta
    {
        public string Ticket { get; set; } = "";
        public DateTime FechaHora { get; set; } = DateTime.Now;
        public string MesaEtiqueta { get; set; } = "";
        public string Cliente { get; set; } = "";
        public string Mozo { get; set; } = "";
        public string MetodoPago { get; set; } = "";
        public string Comprobante { get; set; } = "Boleta";
        public List<ItemVenta> Items { get; set; } = new();
        public double Total { get; set; }
        public string Estado { get; set; } = "PAGADO";
        // Fecha real del cobro: al cobrar un POR COBRAR se fija a DateTime.Now.
        // Los KPIs de ingresos usan esta fecha para que lo cobrado entre hoy.
        public DateTime? FechaCobro { get; set; }
        public string MesaBadgeBg { get; set; } = "";
        public string MesaBadgeBorde { get; set; } = "";
        public string MesaBadgeFg { get; set; } = "";

        [JsonIgnore]
        public bool EsPagada => string.Equals((Estado ?? "").Trim(), "PAGADO", StringComparison.OrdinalIgnoreCase);

        [JsonIgnore]
        public bool EsPorCobrar => string.Equals((Estado ?? "").Trim(), "POR COBRAR", StringComparison.OrdinalIgnoreCase)
            || string.Equals((Estado ?? "").Trim(), "EN MESA", StringComparison.OrdinalIgnoreCase);

        // Fecha efectiva para filtros/KPIs: lo pagado cuenta el dia del cobro.
        [JsonIgnore]
        public DateTime FechaEfectiva => EsPagada ? (FechaCobro ?? FechaHora) : FechaHora;
    }
}
