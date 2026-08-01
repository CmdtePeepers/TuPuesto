using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TuTienda.Data;
using TuTienda.Models.Entities;

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

        // Determina quién es Cliente y quién es Vendedor entre el usuario actual y el otro.
        // Solo se permite chatear Cliente <-> Vendedor (no Cliente-Cliente ni Vendedor-Vendedor).
        private async Task<(int clienteId, int vendedorId)?> ResolverParticipantes(int otroUsuarioId)
        {
            var miId = ObtenerUsuarioId();
            var yo = await _context.Usuarios.FindAsync(miId);
            var otro = await _context.Usuarios.FindAsync(otroUsuarioId);

            if (yo == null || otro == null)
            {
                return null;
            }

            if (yo.RolId == 3 && otro.RolId == 2) return (yo.Id, otro.Id);   // yo Cliente, otro Vendedor
            if (yo.RolId == 2 && otro.RolId == 3) return (otro.Id, yo.Id);   // yo Vendedor, otro Cliente

            return null; // combinación no permitida
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

            // Agrupamos por "el otro participante" y tomamos el último mensaje de cada uno
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

        // GET: Mensaje/Conversacion/5 (5 = Id del otro usuario)
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

            // Marcamos como leídos los que me llegaron a mí
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

            // Mensaje sugerido si viene de "Contactar sobre producto" o "Anular pedido"
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

        // POST: Mensaje/Enviar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enviar(int otroUsuarioId, string contenido, int? productoId, int? pedidoId)
        {
            if (string.IsNullOrWhiteSpace(contenido))
            {
                return RedirectToAction(nameof(Conversacion), new { id = otroUsuarioId, productoId, pedidoId });
            }

            var participantes = await ResolverParticipantes(otroUsuarioId);
            if (participantes == null)
            {
                return Forbid();
            }

            var (clienteId, vendedorId) = participantes.Value;

            _context.Mensajes.Add(new Mensaje
            {
                ClienteId = clienteId,
                VendedorId = vendedorId,
                EmisorId = ObtenerUsuarioId(),
                Contenido = contenido.Trim(),
                ProductoId = productoId,
                PedidoId = pedidoId
            });

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Conversacion), new { id = otroUsuarioId });
        }
    }
}
