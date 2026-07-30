namespace TuTienda.Models.ViewModels
{
    public class VentaPorDiaVM
    {
        public DateTime Fecha { get; set; }
        public int CantidadPedidos { get; set; }
        public decimal Total { get; set; }
    }

    public class ProductoMasVendidoVM
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int CantidadVendida { get; set; }
        public decimal Ingresos { get; set; }
    }

    public class IngresoPorVendedorVM
    {
        public int VendedorId { get; set; }
        public string NombreVendedor { get; set; } = string.Empty;
        public int CantidadPedidos { get; set; }
        public decimal Total { get; set; }
    }

    public class VentaPorMesVM
    {
        public int Mes { get; set; }
        public int CantidadPedidos { get; set; }
        public decimal Total { get; set; }

        public string NombreMes => new DateTime(2000, Mes, 1).ToString("MMMM", new System.Globalization.CultureInfo("es-PE"));
    }
}
