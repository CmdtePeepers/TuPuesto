using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TuTienda.Models.Entities;
using TuTienda.Data;
using TuTienda.Repository;

namespace TuTienda.Controllers
{
    public class ProductoController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ProductoRepository productoRepository;

        public ProductoController(AppDbContext context, ProductoRepository productoRepository)
        {
            _context = context;
            this.productoRepository = productoRepository;
        }

        // Helper: obtiene el Id del usuario logueado desde la cookie de autenticación
        private int? ObtenerUsuarioId()
        {
            var valor = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(valor, out var id) ? id : null;
        }

        private bool EsAdministrador() => User.IsInRole("Administrador");
        private bool EsVendedor() => User.IsInRole("Vendedor");

        // GET: Producto  -> PÚBLICO, cualquiera puede ver el catálogo
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var lista = await productoRepository.ObtenerProductosPaginados("", 0, 1, 3);
            ViewBag.Nombre = "";
            ViewBag.PrecioMin = 0;
            ViewBag.PaginaActual = 1;
            ViewBag.ElementosPorPagina = 3;
            ViewBag.TotalPaginas = (int)Math.Ceiling((double)lista.totalRegistros / 3);
            ViewBag.TotalRegistros = lista.totalRegistros;
            return View(lista.productos);
        }

        // GET: Producto/ObtenerProductos -> PÚBLICO
        [AllowAnonymous]
        public async Task<IActionResult> ObtenerProductos(string nombre, decimal precioMin, int paginaActual = 1, int elementosPorPagina = 3)
        {
            var lista = await productoRepository.ObtenerProductosPaginados(nombre, precioMin, paginaActual, elementosPorPagina);
            ViewBag.Nombre = nombre;
            ViewBag.PrecioMin = precioMin;
            ViewBag.PaginaActual = paginaActual;
            ViewBag.ElementosPorPagina = elementosPorPagina;
            ViewBag.TotalPaginas = (int)Math.Ceiling((double)lista.totalRegistros / elementosPorPagina);
            ViewBag.TotalRegistros = lista.totalRegistros;
            return View("Index", lista.productos);
        }

        // GET: Producto/Details/5 -> PÚBLICO
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var producto = await _context.Productos
                .Include(p => p.Categoria)
                .Include(p => p.Vendedor)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (producto == null)
            {
                return NotFound();
            }

            return View(producto);
        }

        // GET: Producto/Create -> Vendedor o Administrador
        [Authorize(Roles = "Administrador,Vendedor")]
        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.CategoriaId = new SelectList(_context.Categorias, "Id", "Nombre");

            if (EsAdministrador())
            {
                // El admin sí elige a qué vendedor pertenece el producto
                ViewBag.VendedorId = new SelectList(_context.Usuarios.Where(u => u.RolId == 2), "Id", "Nombres");
            }

            return View();
        }

        // POST: Producto/Create -> Vendedor o Administrador
        [Authorize(Roles = "Administrador,Vendedor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Producto producto)
        {
            // Si es Vendedor (no admin), el producto SIEMPRE se asigna a sí mismo,
            // sin importar qué VendedorId venga en el formulario (evita que alguien
            // manipule el HTML y cree productos a nombre de otro vendedor).
            if (EsVendedor() && !EsAdministrador())
            {
                producto.VendedorId = ObtenerUsuarioId() ?? 0;
                ModelState.Remove(nameof(producto.VendedorId));
            }

            if (ModelState.IsValid)
            {
                await productoRepository.AgregarProducto(producto);
                return RedirectToAction(nameof(Index));
            }

            ViewBag.CategoriaId = new SelectList(_context.Categorias, "Id", "Nombre", producto.CategoriaId);
            if (EsAdministrador())
            {
                ViewBag.VendedorId = new SelectList(_context.Usuarios.Where(u => u.RolId == 2), "Id", "Nombres", producto.VendedorId);
            }
            return View(producto);
        }

        // GET: Producto/Edit/5 -> Vendedor (solo lo suyo) o Administrador (todo)
        [Authorize(Roles = "Administrador,Vendedor")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
            {
                return NotFound();
            }

            if (EsVendedor() && !EsAdministrador() && producto.VendedorId != ObtenerUsuarioId())
            {
                return Forbid(); // No es su producto
            }

            ViewBag.CategoriaId = new SelectList(_context.Categorias, "Id", "Nombre", producto.CategoriaId);
            if (EsAdministrador())
            {
                ViewBag.VendedorId = new SelectList(_context.Usuarios.Where(u => u.RolId == 2), "Id", "Nombres", producto.VendedorId);
            }
            return View(producto);
        }

        // POST: Producto/Edit/5
        [Authorize(Roles = "Administrador,Vendedor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int? id, [Bind("Id,Nombre,Descripcion,Precio,Stock,ImagenUrl,CategoriaId,VendedorId,Activo")] Producto producto)
        {
            if (id != producto.Id)
            {
                return NotFound();
            }

            // Verificamos contra el dueño ORIGINAL guardado en la BD, no contra
            // lo que venga en el formulario (que se puede manipular).
            var productoOriginal = await _context.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (productoOriginal == null)
            {
                return NotFound();
            }

            if (EsVendedor() && !EsAdministrador())
            {
                if (productoOriginal.VendedorId != ObtenerUsuarioId())
                {
                    return Forbid();
                }
                producto.VendedorId = productoOriginal.VendedorId; // no permite reasignarlo a otro vendedor
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(producto);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductoExists(producto.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.CategoriaId = new SelectList(_context.Categorias, "Id", "Nombre", producto.CategoriaId);
            if (EsAdministrador())
            {
                ViewBag.VendedorId = new SelectList(_context.Usuarios.Where(u => u.RolId == 2), "Id", "Nombres", producto.VendedorId);
            }
            return View(producto);
        }

        // GET: Producto/Delete/5
        [Authorize(Roles = "Administrador,Vendedor")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var producto = await _context.Productos
                .Include(p => p.Categoria)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (producto == null)
            {
                return NotFound();
            }

            if (EsVendedor() && !EsAdministrador() && producto.VendedorId != ObtenerUsuarioId())
            {
                return Forbid();
            }

            return View(producto);
        }

        // POST: Producto/Delete/5
        [Authorize(Roles = "Administrador,Vendedor")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
            {
                return NotFound();
            }

            if (EsVendedor() && !EsAdministrador() && producto.VendedorId != ObtenerUsuarioId())
            {
                return Forbid();
            }

            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ProductoExists(int id)
        {
            return _context.Productos.Any(e => e.Id == id);
        }
    }
}
