using System.ComponentModel.DataAnnotations;

namespace TuTienda.Models.Entities
{
    public class Rol
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Nombre { get; set; } = string.Empty; // "Administrador", "Vendedor", "Cliente"

        public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    }
}