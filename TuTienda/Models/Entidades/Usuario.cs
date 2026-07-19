using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TuTienda.Models.Entities;

namespace Tutienda.Models.Entities
{
    public class Usuario
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Nombres { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Apellidos { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public int RolId { get; set; }
        [ForeignKey(nameof(RolId))]
        public Rol? Rol { get; set; }

        public bool Activo { get; set; } = true;

        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        // Solo aplica si el usuario tiene Rol = Vendedor
        [MaxLength(150)]
        public string? NombreTienda { get; set; }

        [MaxLength(500)]
        public string? DescripcionTienda { get; set; }

        // Navegación
        public ICollection<Producto> Productos { get; set; } = new List<Producto>();
        public ICollection<Pedido> PedidosComoCliente { get; set; } = new List<Pedido>();
        public ICollection<Pedido> PedidosComoVendedor { get; set; } = new List<Pedido>();
    }
}