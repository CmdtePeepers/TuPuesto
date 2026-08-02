using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TuTienda.Data;
using TuTienda.Models.Enums;

namespace TuTienda.Controllers
{
    [Authorize]
    public class PedidoController : Controller
    {
        private readonly AppDbContext _context;

        public PedidoController(AppDbContext context)
        {
            _context = context;
        }

        private int? ObtenerUsuarioId()
        {
            var valor = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(valor, out var id) ? id : null;
        }

        private bool EsAdministrador() => User.IsInRole("Administrador");
        private bool EsVendedor() => User.IsInRole("Vendedor");

        // GET: Pedido/MisCompras -> Historial del Cliente logueado
        public async Task<IActionResult> MisCompras()
        {
            var usuarioId = ObtenerUsuarioId();

            var pedidos = await _context.Pedidos
                .Where(p => p.ClienteId == usuarioId)
                .Include(p => p.Vendedor)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .OrderByDescending(p => p.FechaPedido)
                .ToListAsync();

            return View(pedidos);
        }

        // GET: Pedido/Gestionar -> Pedidos recibidos (Vendedor: solo los suyos, Administrador: todos)
        public async Task<IActionResult> Gestionar()
        {
            IQueryable<Models.Entities.Pedido> query = _context.Pedidos
                .Include(p => p.Cliente)
                .Include(p => p.Vendedor)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .OrderByDescending(p => p.FechaPedido);

            var usuarioId = ObtenerUsuarioId();

            if (EsAdministrador())
            {
                // sin filtro: ve todos
            }
            else if (EsVendedor())
            {
                query = query.Where(p => p.VendedorId == usuarioId);
            }
            else
            {
                // Cliente: solo sus propias compras
                query = query.Where(p => p.ClienteId == usuarioId);
            }

            var pedidos = await query.ToListAsync();
            return View(pedidos);
        }


        // POST: Pedido/CambiarEstado
        [Authorize(Roles = "Vendedor,Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(int id, EstadoPedido nuevoEstado)
        {
            var pedido = await _context.Pedidos.FindAsync(id);
            if (pedido == null)
            {
                return NotFound();
            }

            if (!EsAdministrador() && pedido.VendedorId != ObtenerUsuarioId())
            {
                return Forbid();
            }

            // Transiciones permitidas: Pendiente -> Confirmado/Cancelado, Confirmado -> Entregado/Cancelado.
            // Entregado y Cancelado son estados finales.
            var transicionesValidas = new Dictionary<EstadoPedido, EstadoPedido[]>
            {
                [EstadoPedido.Pendiente] = new[] { EstadoPedido.Confirmado, EstadoPedido.Cancelado },
                [EstadoPedido.Confirmado] = new[] { EstadoPedido.Entregado, EstadoPedido.Cancelado },
                [EstadoPedido.Entregado] = Array.Empty<EstadoPedido>(),
                [EstadoPedido.Cancelado] = Array.Empty<EstadoPedido>(),
            };

            if (!transicionesValidas[pedido.Estado].Contains(nuevoEstado))
            {
                TempData["Error"] = $"No se puede cambiar un pedido de \"{pedido.Estado}\" a \"{nuevoEstado}\".";
                return RedirectToAction(nameof(Gestionar));
            }

            pedido.Estado = nuevoEstado;
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = $"Pedido #{pedido.Id} actualizado a \"{nuevoEstado}\".";
            return RedirectToAction(nameof(Gestionar));
        }
    }
}
