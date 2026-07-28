using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TuTienda.Data;
using TuTienda.Models.Entities;

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
    }
}
