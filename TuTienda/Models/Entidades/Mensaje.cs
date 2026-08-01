using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TuTienda.Models.Entities
{
    public class Mensaje
    {
        public int Id { get; set; }

        public int ClienteId { get; set; }
        [ForeignKey(nameof(ClienteId))]
        public Usuario? Cliente { get; set; }

        public int VendedorId { get; set; }
        [ForeignKey(nameof(VendedorId))]
        public Usuario? Vendedor { get; set; }

        // Quién escribió ESTE mensaje puntual (puede ser el Cliente o el Vendedor de la conversación)
        public int EmisorId { get; set; }
        [ForeignKey(nameof(EmisorId))]
        public Usuario? Emisor { get; set; }

        [Required, MaxLength(1000)]
        public string Contenido { get; set; } = string.Empty;

        // Contexto opcional: sobre qué producto o pedido es la consulta
        public int? ProductoId { get; set; }
        [ForeignKey(nameof(ProductoId))]
        public Producto? Producto { get; set; }

        public int? PedidoId { get; set; }
        [ForeignKey(nameof(PedidoId))]
        public Pedido? Pedido { get; set; }

        public DateTime FechaEnvio { get; set; } = DateTime.Now;

        public bool Leido { get; set; } = false;
    }
}
