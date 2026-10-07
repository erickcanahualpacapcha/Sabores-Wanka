using System;
using System.Text.Json.Serialization;

namespace sabores_wanka.Models
{
    // Plato de carta: unidad simple en porciones (nada de fracciones complejas).
    // Estado se deriva del stock: 0 Agotado, <=5 Stock Bajo, resto En Carta.
    public sealed class Plato
    {
        public string Plu { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Categoria { get; set; } = "";
        public string EstacionTag { get; set; } = "caliente";
        public string Descripcion { get; set; } = "";
        public double Precio { get; set; }
        public double Costo { get; set; }
        public int Porciones { get; set; }
        public string Unidad { get; set; } = "porc.";
        public DateTime CreadoEl { get; set; } = DateTime.Now;

        // Foto local del plato (ruta absoluta en Imagenes/Platos; "" = sin foto).
        public string FotoPath { get; set; } = "";

        // Pausado = fuera de carta temporalmente (no se vende), sin tocar el stock.
        public bool Pausado { get; set; }

        [JsonIgnore]
        public string Estado => Pausado ? "Pausado" : Porciones <= 0 ? "Agotado" : Porciones <= 5 ? "Stock Bajo" : "En Carta";
    }
}
