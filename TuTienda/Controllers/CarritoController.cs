using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TuTienda.Data;
using TuTienda.Models.Entities;
using TuTienda.Models.Enums;

namespace TuTienda.Controllers
{
    [AllowAnonymous]
    public class CarritoController : Controller
    {
        private readonly AppDbContext _context;
        private const string CARRITO_COOKIE = "TuTienda_CarritoSession";

        public CarritoController(AppDbContext context)
        {
            _context = context;
        }

        private int? ObtenerUsuarioId()
        {
            var valor = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(valor, out var id) ? id : null;
        }

        // Obtiene el SessionId guardado en la cookie del invitado, o crea uno nuevo
        private string ObtenerOCrearSessionId()
        {
            if (Request.Cookies.TryGetValue(CARRITO_COOKIE, out var sessionId) && !string.IsNullOrEmpty(sessionId))
            {
                return sessionId;
            }

            sessionId = Guid.NewGuid().ToString();
            Response.Cookies.Append(CARRITO_COOKIE, sessionId, new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });
            return sessionId;
        }

        // Busca (o crea) el carrito del usuario logueado o del invitado actual
        private async Task<Carrito> ObtenerOCrearCarrito()
        {
            Carrito? carrito;

            if (User.Identity?.IsAuthenticated == true)
            {
                var usuarioId = ObtenerUsuarioId();
                carrito = await _context.Carritos
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);

                if (carrito == null)
                {
                    carrito = new Carrito { UsuarioId = usuarioId };
                    _context.Carritos.Add(carrito);
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                var sessionId = ObtenerOCrearSessionId();
                carrito = await _context.Carritos
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.SessionId == sessionId);

                if (carrito == null)
                {
                    carrito = new Carrito { SessionId = sessionId };
                    _context.Carritos.Add(carrito);
                    await _context.SaveChangesAsync();
                }
            }

            return carrito;
        }

        // POST: Carrito/Agregar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Agregar(int productoId, int cantidad = 1)
        {
            var producto = await _context.Productos.FindAsync(productoId);
            if (producto == null)
            {
                return NotFound();
            }

            var carrito = await ObtenerOCrearCarrito();

            var item = carrito.Items.FirstOrDefault(i => i.ProductoId == productoId);
            if (item == null)
            {
                _context.CarritoItems.Add(new CarritoItem
                {
                    CarritoId = carrito.Id,
                    ProductoId = productoId,
                    Cantidad = cantidad,
                    PrecioUnitario = producto.Precio
                });
            }
            else
            {
                item.Cantidad += cantidad;
            }

            await _context.SaveChangesAsync();

            // Regla del proyecto: si no está logueado, lo mandamos a Login/Registro.
            // Cuando inicie sesión, el AccountController fusiona este carrito con el suyo
            // y lo regresa aquí mismo gracias al returnUrl.
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Carrito") });
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Carrito
        public async Task<IActionResult> Index()
        {
            var carrito = await ObtenerOCrearCarrito();

            var items = await _context.CarritoItems
                .Where(i => i.CarritoId == carrito.Id)
                .Include(i => i.Producto)
                    .ThenInclude(p => p!.Vendedor)
                .ToListAsync();

            return View(items);
        }

        // POST: Carrito/ActualizarCantidad
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarCantidad(int itemId, int cantidad)
        {
            var item = await _context.CarritoItems.FindAsync(itemId);
            if (item != null && cantidad > 0)
            {
                item.Cantidad = cantidad;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Carrito/Eliminar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int itemId)
        {
            var item = await _context.CarritoItems.FindAsync(itemId);
            if (item != null)
            {
                _context.CarritoItems.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Carrito/Checkout -> Resumen antes de confirmar. Requiere estar logueado.
        [Authorize]
        public async Task<IActionResult> Checkout()
        {
            var usuarioId = ObtenerUsuarioId();
            var carrito = await _context.Carritos.FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);

            if (carrito == null)
            {
                TempData["Error"] = "Tu carrito está vacío.";
                return RedirectToAction(nameof(Index));
            }

            var items = await _context.CarritoItems
                .Where(i => i.CarritoId == carrito.Id)
                .Include(i => i.Producto)
                    .ThenInclude(p => p!.Vendedor)
                .ToListAsync();

            if (!items.Any())
            {
                TempData["Error"] = "Tu carrito está vacío.";
                return RedirectToAction(nameof(Index));
            }

            return View(items);
        }

        // POST: Carrito/ConfirmarPago -> Crea 1 Pedido por vendedor (pago simulado)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarPago()
        {
            var usuarioId = ObtenerUsuarioId();
            if (usuarioId == null)
            {
                return Forbid();
            }

            var carrito = await _context.Carritos.FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);
            if (carrito == null)
            {
                TempData["Error"] = "Tu carrito está vacío.";
                return RedirectToAction(nameof(Index));
            }

            var items = await _context.CarritoItems
                .Where(i => i.CarritoId == carrito.Id)
                .Include(i => i.Producto)
                .ToListAsync();

            if (!items.Any())
            {
                TempData["Error"] = "Tu carrito está vacío.";
                return RedirectToAction(nameof(Index));
            }

            // Verificamos stock antes de procesar cualquier cosa
            foreach (var item in items)
            {
                if (item.Producto == null || item.Cantidad > item.Producto.Stock)
                {
                    TempData["Error"] = $"No hay stock suficiente de \"{item.Producto?.Nombre}\". Ajusta la cantidad en tu carrito.";
                    return RedirectToAction(nameof(Index));
                }
            }

            var grupoCompraId = Guid.NewGuid();
            var pedidosCreados = new List<Pedido>();

            // Un Pedido por cada vendedor distinto presente en el carrito
            var itemsPorVendedor = items.GroupBy(i => i.Producto!.VendedorId);

            foreach (var grupo in itemsPorVendedor)
            {
                var pedido = new Pedido
                {
                    GrupoCompraId = grupoCompraId,
                    ClienteId = usuarioId.Value,
                    VendedorId = grupo.Key,
                    Estado = EstadoPedido.Pendiente,
                    MetodoPagoSimulado = "Pago simulado",
                    Total = grupo.Sum(i => i.Cantidad * i.PrecioUnitario)
                };

                foreach (var item in grupo)
                {
                    pedido.Detalles.Add(new DetallePedido
                    {
                        ProductoId = item.ProductoId,
                        Cantidad = item.Cantidad,
                        PrecioUnitario = item.PrecioUnitario,
                        Subtotal = item.Cantidad * item.PrecioUnitario
                    });

                    // Descontamos stock
                    item.Producto!.Stock -= item.Cantidad;
                }

                _context.Pedidos.Add(pedido);
                pedidosCreados.Add(pedido);
            }

            // Vaciamos el carrito
            _context.CarritoItems.RemoveRange(items);

            await _context.SaveChangesAsync();

            TempData["PedidosCreadosIds"] = string.Join(",", pedidosCreados.Select(p => p.Id));
            return RedirectToAction(nameof(Confirmacion));
        }

        // GET: Carrito/Confirmacion
        [Authorize]
        public async Task<IActionResult> Confirmacion()
        {
            var idsTexto = TempData["PedidosCreadosIds"] as string;
            if (string.IsNullOrEmpty(idsTexto))
            {
                return RedirectToAction(nameof(Index));
            }

            var ids = idsTexto.Split(',').Select(int.Parse).ToList();
            var pedidos = await _context.Pedidos
                .Where(p => ids.Contains(p.Id))
                .Include(p => p.Vendedor)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .ToListAsync();

            return View(pedidos);
        }
    }
}
