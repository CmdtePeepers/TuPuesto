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

        // GET: Pedido/Gestionar
        // Vendedor: ve solo los pedidos que le hicieron a él y puede cambiar el estado.
        // Cliente: ve solo sus propios pedidos, en modo solo-lectura.
        public async Task<IActionResult> Gestionar(string? estado = null)
        {
            if (User.IsInRole("Administrador"))
            {
                return Forbid();
            }

            var usuarioId = ObtenerUsuarioId();

            IQueryable<Models.Entities.Pedido> query = _context.Pedidos
                .Include(p => p.Cliente)
                .Include(p => p.Vendedor)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .OrderByDescending(p => p.FechaPedido);

            query = EsVendedor()
                ? query.Where(p => p.VendedorId == usuarioId)
                : query.Where(p => p.ClienteId == usuarioId);

            // Filtro opcional por estado (dropdown en la vista)
            if (!string.IsNullOrEmpty(estado) && Enum.TryParse<EstadoPedido>(estado, out var estadoFiltro))
            {
                query = query.Where(p => p.Estado == estadoFiltro);
            }

            ViewBag.EstadoSeleccionado = estado ?? "";

            var pedidos = await query.ToListAsync();
            return View(pedidos);
        }

        // POST: Pedido/CambiarEstado -> Exclusivo del Vendedor dueño del pedido
        [Authorize(Roles = "Vendedor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(int id, EstadoPedido nuevoEstado)
        {
            var pedido = await _context.Pedidos
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pedido == null)
            {
                return NotFound();
            }

            if (pedido.VendedorId != ObtenerUsuarioId())
            {
                return Forbid();
            }

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

            // Al cancelar, devolvemos el stock reservado en el checkout de vuelta al inventario del vendedor
            if (nuevoEstado == EstadoPedido.Cancelado)
            {
                foreach (var detalle in pedido.Detalles)
                {
                    if (detalle.Producto != null)
                    {
                        detalle.Producto.Stock += detalle.Cantidad;
                    }
                }
            }

            pedido.Estado = nuevoEstado;
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = nuevoEstado == EstadoPedido.Cancelado
                ? $"Pedido #{pedido.Id} cancelado. El stock de sus productos fue restaurado."
                : $"Pedido #{pedido.Id} actualizado a \"{nuevoEstado}\".";

            return RedirectToAction(nameof(Gestionar));
        }
    }
    }