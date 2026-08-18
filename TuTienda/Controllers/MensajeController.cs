using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TuTienda.Data;

namespace TuTienda.Controllers
{
    [Authorize(Roles = "Cliente,Vendedor")]
    public class MensajeController : Controller
    {
        private readonly AppDbContext _context;

        public MensajeController(AppDbContext context)
        {
            _context = context;
        }

        private int ObtenerUsuarioId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        private async Task<(int clienteId, int vendedorId)?> ResolverParticipantes(int otroUsuarioId)
        {
            var miId = ObtenerUsuarioId();
            var yo = await _context.Usuarios.FindAsync(miId);
            var otro = await _context.Usuarios.FindAsync(otroUsuarioId);

            if (yo == null || otro == null)
            {
                return null;
            }

            if (yo.RolId == 3 && otro.RolId == 2) return (yo.Id, otro.Id);
            if (yo.RolId == 2 && otro.RolId == 3) return (otro.Id, yo.Id);

            return null;
        }

        // GET: Mensaje -> lista de conversaciones del usuario logueado
        public async Task<IActionResult> Index()
        {
            var miId = ObtenerUsuarioId();
            var esCliente = User.IsInRole("Cliente");

            var mensajes = await _context.Mensajes
                .Where(m => m.ClienteId == miId || m.VendedorId == miId)
                .Include(m => m.Cliente)
                .Include(m => m.Vendedor)
                .OrderByDescending(m => m.FechaEnvio)
                .ToListAsync();

            var conversaciones = mensajes
                .GroupBy(m => esCliente ? m.VendedorId : m.ClienteId)
                .Select(g => new
                {
                    OtroUsuarioId = g.Key,
                    OtroUsuarioNombre = esCliente
                        ? (g.First().Vendedor!.NombreTienda ?? g.First().Vendedor!.Nombres)
                        : (g.First().Cliente!.Nombres + " " + g.First().Cliente!.Apellidos),
                    UltimoMensaje = g.First().Contenido,
                    Fecha = g.First().FechaEnvio,
                    NoLeidos = g.Count(m => !m.Leido && m.EmisorId != miId)
                })
                .OrderByDescending(c => c.Fecha)
                .ToList();

            ViewBag.Conversaciones = conversaciones;
            return View();
        }

        // GET: Mensaje/Conversacion/5
        public async Task<IActionResult> Conversacion(int id, int? productoId, int? pedidoId)
        {
            var participantes = await ResolverParticipantes(id);
            if (participantes == null)
            {
                return NotFound();
            }

            var (clienteId, vendedorId) = participantes.Value;
            var miId = ObtenerUsuarioId();

            var mensajes = await _context.Mensajes
                .Where(m => m.ClienteId == clienteId && m.VendedorId == vendedorId)
                .Include(m => m.Producto)
                .Include(m => m.Pedido)
                .OrderBy(m => m.FechaEnvio)
                .ToListAsync();

            var noLeidos = mensajes.Where(m => !m.Leido && m.EmisorId != miId).ToList();
            foreach (var m in noLeidos)
            {
                m.Leido = true;
            }
            if (noLeidos.Any())
            {
                await _context.SaveChangesAsync();
            }

            var otroUsuario = await _context.Usuarios.FindAsync(id);

            ViewBag.OtroUsuarioId = id;
            ViewBag.OtroUsuarioNombre = otroUsuario?.NombreTienda ?? $"{otroUsuario?.Nombres} {otroUsuario?.Apellidos}";
            ViewBag.MiId = miId;
            ViewBag.ProductoId = productoId;
            ViewBag.PedidoId = pedidoId;

            if (productoId.HasValue)
            {
                var producto = await _context.Productos.FindAsync(productoId.Value);
                ViewBag.MensajeSugerido = producto != null
                    ? $"Hola, quiero más información sobre el producto \"{producto.Nombre}\"."
                    : null;
            }
            else if (pedidoId.HasValue)
            {
                ViewBag.MensajeSugerido = $"Hola, quiero anular mi pedido #{pedidoId.Value}.";
            }

            return View(mensajes);
        }
    }
}
