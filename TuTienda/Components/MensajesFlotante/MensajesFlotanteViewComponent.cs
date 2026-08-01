using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TuTienda.Data;

namespace TuTienda.Components
{
    public class MensajesFlotanteViewComponent : ViewComponent
    {
        private readonly AppDbContext _context;

        public MensajesFlotanteViewComponent(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var usuario = HttpContext.User;

            // Solo se muestra si hay sesión iniciada y es Cliente o Vendedor
            if (usuario.Identity == null || !usuario.Identity.IsAuthenticated)
            {
                return Content(string.Empty);
            }

            bool esCliente = usuario.IsInRole("Cliente");
            bool esVendedor = usuario.IsInRole("Vendedor");

            if (!esCliente && !esVendedor)
            {
                return Content(string.Empty);
            }

            var miId = int.Parse(usuario.FindFirstValue(ClaimTypes.NameIdentifier)!);

            int noLeidos = await _context.Mensajes
                .Where(m => (esCliente ? m.ClienteId == miId : m.VendedorId == miId)
                            && !m.Leido
                            && m.EmisorId != miId)
                .CountAsync();

            return View(noLeidos);
        }
    }
}
