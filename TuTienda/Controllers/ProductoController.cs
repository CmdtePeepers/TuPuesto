using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
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

        // GET: Producto
        [HttpGet]
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

        // GET: Producto/ObtenerProductos (filtro + paginación)
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

        // GET: Producto/Details/5
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
        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.CategoriaId = new SelectList(_context.Categorias, "Id", "Nombre");
            ViewBag.VendedorId = new SelectList(_context.Usuarios, "Id", "Nombres");
            return View();
        }

        // POST: Producto/Create  (usa el Repository, igual que el profesor)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Producto producto)
        {
            if (ModelState.IsValid)
            {
                await productoRepository.AgregarProducto(producto);
                return RedirectToAction(nameof(Index));
            }
            ViewBag.CategoriaId = new SelectList(_context.Categorias, "Id", "Nombre", producto.CategoriaId);
            ViewBag.VendedorId = new SelectList(_context.Usuarios, "Id", "Nombres", producto.VendedorId);
            return View(producto);
        }

        // GET: Producto/Edit/5
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
            ViewBag.CategoriaId = new SelectList(_context.Categorias, "Id", "Nombre", producto.CategoriaId);
            ViewBag.VendedorId = new SelectList(_context.Usuarios, "Id", "Nombres", producto.VendedorId);
            return View(producto);
        }

        // POST: Producto/Edit/5  (este sigue usando EF Core directo, como el profesor)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int? id, [Bind("Id,Nombre,Descripcion,Precio,Stock,ImagenUrl,CategoriaId,VendedorId,Activo")] Producto producto)
        {
            if (id != producto.Id)
            {
                return NotFound();
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
            ViewBag.VendedorId = new SelectList(_context.Usuarios, "Id", "Nombres", producto.VendedorId);
            return View(producto);
        }

        // GET: Producto/Delete/5
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

            return View(producto);
        }

        // POST: Producto/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto != null)
            {
                _context.Productos.Remove(producto);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ProductoExists(int id)
        {
            return _context.Productos.Any(e => e.Id == id);
        }
    }
}
