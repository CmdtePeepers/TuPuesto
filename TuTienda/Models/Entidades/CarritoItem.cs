using System.ComponentModel.DataAnnotations.Schema;

namespace Tutienda.Models.Entities
{
    public class CarritoItem
    {
        public int Id { get; set; }

        public int CarritoId { get; set; }
        [ForeignKey(nameof(CarritoId))]
        public Carrito? Carrito { get; set; }

        public int ProductoId { get; set; }
        [ForeignKey(nameof(ProductoId))]
        public Producto? Producto { get; set; }

        public int Cantidad { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal PrecioUnitario { get; set; } // precio congelado al momento de agregarlo
    }
}