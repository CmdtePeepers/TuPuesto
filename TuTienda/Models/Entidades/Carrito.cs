using System.ComponentModel.DataAnnotations.Schema;
using TuTienda.Models.Entities;

namespace Tutienda.Models.Entities
{
    public class Carrito
    {
        public int Id { get; set; }

        public int? UsuarioId { get; set; }
        [ForeignKey(nameof(UsuarioId))]
        public Usuario? Usuario { get; set; }

        public string? SessionId { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public ICollection<CarritoItem> Items { get; set; } = new List<CarritoItem>();
    }
}
