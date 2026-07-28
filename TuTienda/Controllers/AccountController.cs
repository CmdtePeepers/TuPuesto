using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TuTienda.Data;
using TuTienda.Models.Entities;
using TuTienda.Models.ViewModels;

namespace TuTienda.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<Usuario> _hasher = new();

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // POST: Account/Register
        // El auto-registro SIEMPRE crea un Cliente (RolId = 3).
        // Los Vendedores los crea el Administrador manualmente desde /Usuario/Create.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            bool existe = await _context.Usuarios.AnyAsync(u => u.Email == model.Email);
            if (existe)
            {
                ModelState.AddModelError(nameof(model.Email), "Ya existe una cuenta con ese correo.");
                return View(model);
            }

            var usuario = new Usuario
            {
                Nombres = model.Nombres,
                Apellidos = model.Apellidos,
                Email = model.Email,
                RolId = 3, // Cliente
                Activo = true
            };
            usuario.PasswordHash = _hasher.HashPassword(usuario, model.Password);

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            await IniciarSesion(usuario);

            return RedirectToAction("Index", "Home");
        }

        // GET: Account/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ReturnUrl = returnUrl;
                return View(model);
            }

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == model.Email);

            if (usuario == null || !usuario.Activo)
            {
                ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
                ViewBag.ReturnUrl = returnUrl;
                return View(model);
            }

            var resultado = _hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, model.Password);
            if (resultado == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
                ViewBag.ReturnUrl = returnUrl;
                return View(model);
            }

            await IniciarSesion(usuario);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        // POST: Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        private async Task IniciarSesion(Usuario usuario)
        {
            string nombreRol = usuario.RolId switch
            {
                1 => "Administrador",
                2 => "Vendedor",
                _ => "Cliente"
            };

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, $"{usuario.Nombres} {usuario.Apellidos}"),
                new Claim(ClaimTypes.Email, usuario.Email),
                new Claim(ClaimTypes.Role, nombreRol)
            };

            var identidad = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identidad);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }
    }
}
