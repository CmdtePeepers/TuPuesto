using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;
using TuTienda.Data;
using TuTienda.Models;
using TuTienda.Models.ViewModels;
using TuTienda.Models.Enums;

namespace TuTienda.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private const int PRODUCTOS_POR_CATEGORIA = 4;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            bool autenticado = User.Identity != null && User.Identity.IsAuthenticated;
            bool esVendedor = autenticado && User.IsInRole("Vendedor");
            bool esAdministrador = autenticado && User.IsInRole("Administrador");

            if (esVendedor)
            {
                var vendedorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                ViewBag.EsVendedor = true;
                ViewBag.TotalProductos = await _context.Productos.CountAsync(p => p.VendedorId == vendedorId);
                ViewBag.ProductosActivos = await _context.Productos.CountAsync(p => p.VendedorId == vendedorId && p.Activo);
                ViewBag.PedidosPendientes = await _context.Pedidos.CountAsync(p => p.VendedorId == vendedorId && p.Estado == EstadoPedido.Pendiente);

                return View(new List<CategoriaConProductos>());
            }

            if (esAdministrador)
            {
                ViewBag.EsAdministrador = true;
                ViewBag.TotalUsuarios = await _context.Usuarios.CountAsync();
                ViewBag.TotalCategorias = await _context.Categorias.CountAsync();
                ViewBag.MensajesPendientes = await _context.Contactos.CountAsync(c => !c.Atendido);

                return View(new List<CategoriaConProductos>());
            }

            // Cliente / Visitante: catálogo con buscador
            var categorias = await _context.Categorias
                .Where(c => c.Productos.Any(p => p.Activo))
                .OrderBy(c => c.Nombre)
                .ToListAsync();

            var secciones = new List<CategoriaConProductos>();
            foreach (var categoria in categorias)
            {
                var productos = await _context.Productos
                    .Where(p => p.Activo && p.CategoriaId == categoria.Id)
                    .OrderByDescending(p => p.FechaCreacion)
                    .Take(PRODUCTOS_POR_CATEGORIA)
                    .ToListAsync();

                secciones.Add(new CategoriaConProductos { Categoria = categoria, Productos = productos });
            }

            ViewBag.CategoriasFiltro = new SelectList(_context.Categorias.OrderBy(c => c.Nombre), "Id", "Nombre");

            return View(secciones);
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