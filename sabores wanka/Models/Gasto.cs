using System;

namespace sabores_wanka.Models
{
    // Gasto/egreso de caja (compras, servicios, planilla). Una fila por registro.
    public sealed class Gasto
    {
        public string Comprobante { get; set; } = "";
        public DateTime FechaHora { get; set; } = DateTime.Now;
        public string Tipo { get; set; } = "COMPRA";
        public string Concepto { get; set; } = "";
        public string Autorizo { get; set; } = "";
        public double Monto { get; set; }
        public string Metodo { get; set; } = "";
        public string Nota { get; set; } = "";
        public string Estado { get; set; } = "EGRESO";
    }
}
