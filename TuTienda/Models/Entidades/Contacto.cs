using System.ComponentModel.DataAnnotations;

namespace TuTienda.Models.Entities
{
    public class Contacto
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tu nombre es obligatorio.")]
        [MaxLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tu correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Correo no válido.")]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Telefono { get; set; }

        [Required(ErrorMessage = "Selecciona un motivo.")]
        [MaxLength(100)]
        public string Motivo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Cuéntanos brevemente tu consulta.")]
        [MaxLength(1000)]
        public string Mensaje { get; set; } = string.Empty;

        public DateTime FechaEnvio { get; set; } = DateTime.Now;

        public bool Atendido { get; set; } = false;
    }
}