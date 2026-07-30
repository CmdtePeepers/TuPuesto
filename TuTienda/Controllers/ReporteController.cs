using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuTienda.Repository;

namespace TuTienda.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class ReporteController : Controller
    {
        private readonly ReporteRepository _reporteRepository;

        public ReporteController(ReporteRepository reporteRepository)
        {
            _reporteRepository = reporteRepository;
        }

        // GET: Reporte -> Panel con acceso a los 4 reportes
        public IActionResult Index()
        {
            return View();
        }

        // GET: Reporte/VentasPorPeriodo
        public async Task<IActionResult> VentasPorPeriodo(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var fin = fechaFin ?? DateTime.Today;
            var inicio = fechaInicio ?? fin.AddDays(-30);

            var datos = await _reporteRepository.VentasPorPeriodo(inicio, fin);

            ViewBag.FechaInicio = inicio.ToString("yyyy-MM-dd");
            ViewBag.FechaFin = fin.ToString("yyyy-MM-dd");
            ViewBag.TotalGeneral = datos.Sum(d => d.Total);
            ViewBag.TotalPedidos = datos.Sum(d => d.CantidadPedidos);

            return View(datos);
        }

        // GET: Reporte/ProductosMasVendidos
        public async Task<IActionResult> ProductosMasVendidos(DateTime? fechaInicio, DateTime? fechaFin, int topN = 10)
        {
            var datos = await _reporteRepository.ProductosMasVendidos(fechaInicio, fechaFin, topN);

            ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
            ViewBag.FechaFin = fechaFin?.ToString("yyyy-MM-dd");
            ViewBag.TopN = topN;

            return View(datos);
        }

        // GET: Reporte/IngresosPorVendedor
        public async Task<IActionResult> IngresosPorVendedor(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var datos = await _reporteRepository.IngresosPorVendedor(fechaInicio, fechaFin);

            ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
            ViewBag.FechaFin = fechaFin?.ToString("yyyy-MM-dd");
            ViewBag.TotalGeneral = datos.Sum(d => d.Total);

            return View(datos);
        }

        // GET: Reporte/ComparacionMensual
        public async Task<IActionResult> ComparacionMensual(int? anio)
        {
            var anioSeleccionado = anio ?? DateTime.Today.Year;
            var datos = await _reporteRepository.ComparacionMensual(anioSeleccionado);

            ViewBag.Anio = anioSeleccionado;

            return View(datos);
        }
    }
}
