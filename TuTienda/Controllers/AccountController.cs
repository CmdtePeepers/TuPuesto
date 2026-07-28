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
        private const string CARRITO_COOKIE = "TuTienda_CarritoSession";
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
        public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
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
                RolId = 3,
                Activo = true
            };
            usuario.PasswordHash = _hasher.HashPassword(usuario, model.Password);

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            await IniciarSesion(usuario);
            await FusionarCarritoDeInvitado(usuario);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
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

            PasswordVerificationResult resultado;
            try
            {
                resultado = _hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, model.Password);
            }
            catch (FormatException)
            {
                // El hash guardado no tiene un formato válido (ej: se insertó texto plano a mano)
                ModelState.AddModelError(string.Empty, "Esta cuenta tiene un problema con su contraseña. Contacta al administrador.");
                ViewBag.ReturnUrl = returnUrl;
                return View(model);
            }

            if (resultado == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
                ViewBag.ReturnUrl = returnUrl;
                return View(model);
            }

            await IniciarSesion(usuario);
            await FusionarCarritoDeInvitado(usuario);

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
        // Une el carrito que se armó como invitado (identificado por cookie) con el
        // carrito del usuario que se acaba de loguear/registrar.
        private async Task FusionarCarritoDeInvitado(Usuario usuario)
        {
            if (!Request.Cookies.TryGetValue(CARRITO_COOKIE, out var sessionId) || string.IsNullOrEmpty(sessionId))
            {
                return;
            }

            var carritoInvitado = await _context.Carritos
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.SessionId == sessionId);

            if (carritoInvitado == null)
            {
                return;
            }

            var carritoUsuario = await _context.Carritos
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UsuarioId == usuario.Id);

            if (carritoUsuario == null)
            {
                // El usuario no tenía carrito propio: el de invitado pasa a ser el suyo
                carritoInvitado.UsuarioId = usuario.Id;
                carritoInvitado.SessionId = null;
            }
            else
            {
                // Ya tenía carrito: fusionamos los items (sumando cantidades si se repite el producto)
                foreach (var item in carritoInvitado.Items.ToList())
                {
                    var existente = carritoUsuario.Items.FirstOrDefault(i => i.ProductoId == item.ProductoId);
                    if (existente != null)
                    {
                        existente.Cantidad += item.Cantidad;
                        _context.CarritoItems.Remove(item);
                    }
                    else
                    {
                        item.CarritoId = carritoUsuario.Id;
                    }
                }
                _context.Carritos.Remove(carritoInvitado);
            }

            await _context.SaveChangesAsync();
            Response.Cookies.Delete(CARRITO_COOKIE);
        }
    }
}
