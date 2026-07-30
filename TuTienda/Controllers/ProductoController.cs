using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TuTienda.Models.Entities;
using TuTienda.Models.ViewModels;
using TuTienda.Data;
using TuTienda.Repository;

namespace TuTienda.Controllers
{
    public class ProductoController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ProductoRepository productoRepository;
        private const int PRODUCTOS_POR_CATEGORIA_EN_HOME = 4;

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

        // GET: Producto  -> Vitrina: buscador + 4 productos por categoría
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
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
                    .Take(PRODUCTOS_POR_CATEGORIA_EN_HOME)
                    .ToListAsync();

                secciones.Add(new CategoriaConProductos
                {
                    Categoria = categoria,
                    Productos = productos
                });
            }

            return View(secciones);
        }

        // GET: Producto/Categoria/5 -> Todos los productos de una categoría (paginado)
        [AllowAnonymous]
        public async Task<IActionResult> Categoria(int id, string? nombre, decimal precioMin = 0, int paginaActual = 1, int elementosPorPagina = 8)
        {
            var categoria = await _context.Categorias.FindAsync(id);
            if (categoria == null)
            {
                return NotFound();
            }

            var lista = await productoRepository.ObtenerProductosPaginados(nombre ?? "", precioMin, id, paginaActual, elementosPorPagina);

            ViewBag.Titulo = categoria.Nombre;
            ViewBag.CategoriaId = id;
            ViewBag.Nombre = nombre;
            ViewBag.PrecioMin = precioMin;
            ViewBag.PaginaActual = paginaActual;
            ViewBag.ElementosPorPagina = elementosPorPagina;
            ViewBag.TotalPaginas = (int)Math.Ceiling((double)lista.totalRegistros / elementosPorPagina);
            ViewBag.TotalRegistros = lista.totalRegistros;

            return View("Listado", lista.productos);
        }

        // GET: Producto/Buscar -> Resultados de búsqueda en TODAS las categorías
        [AllowAnonymous]
        public async Task<IActionResult> Buscar(string nombre, decimal precioMin = 0, int paginaActual = 1, int elementosPorPagina = 8)
        {
            var lista = await productoRepository.ObtenerProductosPaginados(nombre ?? "", precioMin, 0, paginaActual, elementosPorPagina);

            ViewBag.Titulo = string.IsNullOrWhiteSpace(nombre) ? "Todos los productos" : $"Resultados para \"{nombre}\"";
            ViewBag.CategoriaId = 0;
            ViewBag.Nombre = nombre;
            ViewBag.PrecioMin = precioMin;
            ViewBag.PaginaActual = paginaActual;
            ViewBag.ElementosPorPagina = elementosPorPagina;
            ViewBag.TotalPaginas = (int)Math.Ceiling((double)lista.totalRegistros / elementosPorPagina);
            ViewBag.TotalRegistros = lista.totalRegistros;

            return View("Listado", lista.productos);
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

        // GET: Producto/Edit/5
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
                return Forbid();
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
                producto.VendedorId = productoOriginal.VendedorId;
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
