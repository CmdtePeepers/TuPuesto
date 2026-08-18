using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;
using TuTienda.Data;
using TuTienda.Models;
using TuTienda.Models.Entities;
using TuTienda.Models.Enums;

namespace TuTienda.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            bool esVendedor = User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("Vendedor");

            if (esVendedor)
            {
                var vendedorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                ViewBag.EsVendedor = true;
                ViewBag.TotalProductos = await _context.Productos.CountAsync(p => p.VendedorId == vendedorId);
                ViewBag.ProductosActivos = await _context.Productos.CountAsync(p => p.VendedorId == vendedorId && p.Activo);
                ViewBag.PedidosPendientes = await _context.Pedidos.CountAsync(p => p.VendedorId == vendedorId && p.Estado == EstadoPedido.Pendiente);

                return View(new List<Producto>());
            }

            var productos = await _context.Productos
                .Where(p => p.Activo)
                .Include(p => p.Categoria)
                .OrderByDescending(p => p.FechaCreacion)
                .Take(8)
                .ToListAsync();

            return View(productos);
        }

        public IActionResult Nosotros()
        {
            bool esAdminOVendedor = User.Identity != null && User.Identity.IsAuthenticated
                && (User.IsInRole("Administrador") || User.IsInRole("Vendedor"));

            if (esAdminOVendedor)
            {
                TempData["Error"] = "Esta sección solo está disponible para clientes.";
                return RedirectToAction(nameof(Index));
            }

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}