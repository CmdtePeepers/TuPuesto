
using System.ComponentModel.DataAnnotations.Schema;
using TuTienda.Models.Enums;

namespace TuTienda.Models.Entities

{
    public class Pedido
    {
        public int Id { get; set; }

        public Guid GrupoCompraId { get; set; }

        public int ClienteId { get; set; }
        [ForeignKey(nameof(ClienteId))]
        public Usuario? Cliente { get; set; }

        public int VendedorId { get; set; }
        [ForeignKey(nameof(VendedorId))]
        public Usuario? Vendedor { get; set; }

        public DateTime FechaPedido { get; set; } = DateTime.Now;

        public EstadoPedido Estado { get; set; } = EstadoPedido.Pendiente;

        [Column(TypeName = "decimal(10,2)")]
        public decimal Total { get; set; }

        public string MetodoPagoSimulado { get; set; } = "Pago simulado";

        public ICollection<DetallePedido> Detalles { get; set; } = new List<DetallePedido>();
    }
}
