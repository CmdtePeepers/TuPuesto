using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TuTienda.Data;
using TuTienda.Models.Entities;

namespace TuTienda.Controllers
{
    public class ContactoController : Controller
    {
        private readonly AppDbContext _context;

        public ContactoController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Contacto/Contactanos -> formulario público
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Contactanos()
        {
            return View(new Contacto());
        }

        // POST: Contacto/Contactanos
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contactanos(Contacto contacto)
        {
            // Evitamos que puedan mandar campos que no deberían fijar ellos mismos
            ModelState.Remove(nameof(contacto.FechaEnvio));
            ModelState.Remove(nameof(contacto.Atendido));

            if (ModelState.IsValid)
            {
                contacto.FechaEnvio = DateTime.Now;
                contacto.Atendido = false;

                _context.Contactos.Add(contacto);
                await _context.SaveChangesAsync();

                TempData["Mensaje"] = "¡Gracias por escribirnos! Tu mensaje llegó al equipo de TuTienda, te contactaremos pronto.";
                return RedirectToAction(nameof(Contactanos));
            }

            return View(contacto);
        }

        // GET: Contacto -> bandeja del Administrador
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Index()
        {
            var contactos = await _context.Contactos
                .OrderByDescending(c => c.FechaEnvio)
                .ToListAsync();
            return View(contactos);
        }

        // POST: Contacto/MarcarAtendido/5
        [Authorize(Roles = "Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarAtendido(int id)
        {
            var contacto = await _context.Contactos.FindAsync(id);
            if (contacto != null)
            {
                contacto.Atendido = !contacto.Atendido;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}