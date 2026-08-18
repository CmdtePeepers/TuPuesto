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

        private int? ObtenerUsuarioId()
        {
            var valor = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(valor, out var id) ? id : null;
        }

        // GET: Producto -> Vitrina pública (solo activos)
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

            // Para el dropdown del buscador principal
            ViewBag.CategoriasFiltro = new SelectList(_context.Categorias.OrderBy(c => c.Nombre), "Id", "Nombre");

            return View(secciones);
        }

        // GET: Producto/Mantenimiento -> Exclusivo del Vendedor, sobre SUS productos
        [Authorize(Roles = "Vendedor")]
        public async Task<IActionResult> Mantenimiento()
        {
            var usuarioId = ObtenerUsuarioId();

            var productos = await _context.Productos
                .Include(p => p.Categoria)
                .Where(p => p.VendedorId == usuarioId)
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            return View(productos);
        }

        // POST: Producto/CambiarActivo/5
        [Authorize(Roles = "Vendedor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarActivo(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
            {
                return NotFound();
            }

            if (producto.VendedorId != ObtenerUsuarioId())
            {
                return Forbid();
            }

            producto.Activo = !producto.Activo;
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = $"\"{producto.Nombre}\" ahora está {(producto.Activo ? "activo" : "inactivo")}.";
            return RedirectToAction(nameof(Mantenimiento));
        }

        // GET: Producto/Categoria/5
        [AllowAnonymous]
        public async Task<IActionResult> Categoria(int id, string? nombre, int paginaActual = 1, int elementosPorPagina = 8)
        {
            var categoria = await _context.Categorias.FindAsync(id);
            if (categoria == null)
            {
                return NotFound();
            }

            var lista = await productoRepository.ObtenerProductosPaginados(nombre ?? "", 0, id, paginaActual, elementosPorPagina);

            ViewBag.Titulo = categoria.Nombre;
            ViewBag.CategoriaId = id;
            ViewBag.Nombre = nombre;
            ViewBag.PaginaActual = paginaActual;
            ViewBag.ElementosPorPagina = elementosPorPagina;
            ViewBag.TotalPaginas = (int)Math.Ceiling((double)lista.totalRegistros / elementosPorPagina);
            ViewBag.TotalRegistros = lista.totalRegistros;
            ViewBag.CategoriasFiltro = new SelectList(_context.Categorias.OrderBy(c => c.Nombre), "Id", "Nombre", id);

            return View("Listado", lista.productos);
        }

        // GET: Producto/Buscar -> combina nombre + categoría
        [AllowAnonymous]
        public async Task<IActionResult> Buscar(string nombre, int categoriaId = 0, int paginaActual = 1, int elementosPorPagina = 8)
        {
            var lista = await productoRepository.ObtenerProductosPaginados(nombre ?? "", 0, categoriaId, paginaActual, elementosPorPagina);

            string tituloBase = string.IsNullOrWhiteSpace(nombre) ? "Todos los productos" : $"Resultados para \"{nombre}\"";
            if (categoriaId > 0)
            {
                var categoria = await _context.Categorias.FindAsync(categoriaId);
                if (categoria != null)
                {
                    tituloBase += $" en {categoria.Nombre}";
                }
            }

            ViewBag.Titulo = tituloBase;
            ViewBag.CategoriaId = categoriaId;
            ViewBag.Nombre = nombre;
            ViewBag.PaginaActual = paginaActual;
            ViewBag.ElementosPorPagina = elementosPorPagina;
            ViewBag.TotalPaginas = (int)Math.Ceiling((double)lista.totalRegistros / elementosPorPagina);
            ViewBag.TotalRegistros = lista.totalRegistros;
            ViewBag.CategoriasFiltro = new SelectList(_context.Categorias.OrderBy(c => c.Nombre), "Id", "Nombre", categoriaId);

            return View("Listado", lista.productos);
        }

        // GET: Producto/Details/5 -> público
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

        // GET: Producto/Create
        [Authorize(Roles = "Vendedor")]
        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.CategoriaId = new SelectList(_context.Categorias, "Id", "Nombre");
            return View();
        }

        // POST: Producto/Create
        [Authorize(Roles = "Vendedor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Producto producto)
        {
            producto.VendedorId = ObtenerUsuarioId() ?? 0;
            ModelState.Remove(nameof(producto.VendedorId));

            if (ModelState.IsValid)
            {
                await productoRepository.AgregarProducto(producto);
                return RedirectToAction(nameof(Mantenimiento));
            }

            ViewBag.CategoriaId = new SelectList(_context.Categorias, "Id", "Nombre", producto.CategoriaId);
            return View(producto);
        }

        // GET: Producto/Edit/5
        [Authorize(Roles = "Vendedor")]
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

            if (producto.VendedorId != ObtenerUsuarioId())
            {
                return Forbid();
            }

            ViewBag.CategoriaId = new SelectList(_context.Categorias, "Id", "Nombre", producto.CategoriaId);
            return View(producto);
        }

        // POST: Producto/Edit/5
        [Authorize(Roles = "Vendedor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int? id, [Bind("Id,Nombre,Descripcion,Precio,Stock,ImagenUrl,CategoriaId,VendedorId,Activo")] Producto producto)
        {
            if (id != producto.Id)
            {
                return NotFound();
            }

            var productoOriginal = await _context.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (productoOriginal == null)
            {
                return NotFound();
            }

            if (productoOriginal.VendedorId != ObtenerUsuarioId())
            {
                return Forbid();
            }
            producto.VendedorId = productoOriginal.VendedorId;

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
                return RedirectToAction(nameof(Mantenimiento));
            }

            ViewBag.CategoriaId = new SelectList(_context.Categorias, "Id", "Nombre", producto.CategoriaId);
            return View(producto);
        }

        // GET: Producto/Delete/5
        [Authorize(Roles = "Vendedor")]
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

            if (producto.VendedorId != ObtenerUsuarioId())
            {
                return Forbid();
            }

            bool tieneHistorial = await _context.DetallesPedido.AnyAsync(d => d.ProductoId == id)
                || await _context.CarritoItems.AnyAsync(c => c.ProductoId == id)
                || await _context.Mensajes.AnyAsync(m => m.ProductoId == id);

            ViewBag.TieneHistorial = tieneHistorial;

            return View(producto);
        }

        // POST: Producto/Delete/5
        [Authorize(Roles = "Vendedor")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
            {
                return RedirectToAction(nameof(Mantenimiento));
            }

            if (producto.VendedorId != ObtenerUsuarioId())
            {
                return Forbid();
            }

            bool tieneHistorial = await _context.DetallesPedido.AnyAsync(d => d.ProductoId == id)
                || await _context.CarritoItems.AnyAsync(c => c.ProductoId == id)
                || await _context.Mensajes.AnyAsync(m => m.ProductoId == id);

            if (tieneHistorial)
            {
                TempData["Error"] = $"No se puede eliminar \"{producto.Nombre}\" porque ya tiene pedidos, carritos o mensajes asociados. " +
                                     "Puedes desactivarlo en su lugar desde Mantenimiento de Productos.";
                return RedirectToAction(nameof(Mantenimiento));
            }

            try
            {
                _context.Productos.Remove(producto);
                await _context.SaveChangesAsync();
                TempData["Mensaje"] = $"Producto \"{producto.Nombre}\" eliminado correctamente.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = $"No se puede eliminar \"{producto.Nombre}\" porque tiene datos relacionados. Desactívalo en su lugar.";
            }

            return RedirectToAction(nameof(Mantenimiento));
        }

        private bool ProductoExists(int id)
        {
            return _context.Productos.Any(e => e.Id == id);
        }
    }
}