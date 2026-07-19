using System.ComponentModel.DataAnnotations.Schema;

namespace Tutienda.Models.Entities
{
    public class Carrito
    {
        public int Id { get; set; }

        // Nullable: mientras el cliente no se loguea, el carrito no tiene UsuarioId
        public int? UsuarioId { get; set; }
        [ForeignKey(nameof(UsuarioId))]
        public Usuario? Usuario { get; set; }

        // Identificador de la cookie/sesión del navegador para invitados.
        // Se limpia (pasa a null) una vez que el carrito se asocia a un UsuarioId.
        public string? SessionId { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public ICollection<CarritoItem> Items { get; set; } = new List<CarritoItem>();
    }
}
