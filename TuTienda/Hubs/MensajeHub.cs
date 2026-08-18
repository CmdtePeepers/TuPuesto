using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using TuTienda.Data;
using TuTienda.Models.Entities;

namespace TuTienda.Hubs
{
    // Solo Cliente y Vendedor pueden conectarse: la cookie de autenticación
    // que ya usan tus Controllers se reutiliza automáticamente aquí.
    [Authorize(Roles = "Cliente,Vendedor")]
    public class MensajeHub : Hub
    {
        private readonly AppDbContext _context;

        public MensajeHub(AppDbContext context)
        {
            _context = context;
        }

        private int ObtenerUsuarioId()
        {
            return int.Parse(Context.User!.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        }

        // Nombre de grupo determinístico: siempre el mismo sin importar quién lo arme
        private static string NombreGrupoConversacion(int clienteId, int vendedorId) => $"conv-{clienteId}-{vendedorId}";

        // Cada usuario, al conectarse (en CUALQUIER página, gracias al ícono flotante),
        // entra a "su propio" grupo personal. Sirve para avisarle "tienes un mensaje nuevo"
        // aunque no tenga abierta esa conversación específica.
        public override async Task OnConnectedAsync()
        {
            var miId = ObtenerUsuarioId();
            await Groups.AddToGroupAsync(Context.ConnectionId, $"usuario-{miId}");
            await base.OnConnectedAsync();
        }

        // El cliente JS llama esto al abrir la pantalla de una conversación específica
        public async Task UnirseConversacion(int otroUsuarioId)
        {
            var participantes = await ResolverParticipantes(otroUsuarioId);
            if (participantes == null) return;

            var (clienteId, vendedorId) = participantes.Value;
            await Groups.AddToGroupAsync(Context.ConnectionId, NombreGrupoConversacion(clienteId, vendedorId));
        }

        // Reemplaza por completo al antiguo MensajeController.Enviar (POST).
        // Guarda en BD y transmite en vivo SOLO a los 2 participantes de esa conversación.
        public async Task EnviarMensaje(int otroUsuarioId, string contenido, int? productoId, int? pedidoId)
        {
            if (string.IsNullOrWhiteSpace(contenido)) return;

            var participantes = await ResolverParticipantes(otroUsuarioId);
            if (participantes == null) return;

            var (clienteId, vendedorId) = participantes.Value;
            var miId = ObtenerUsuarioId();

            var mensaje = new Mensaje
            {
                ClienteId = clienteId,
                VendedorId = vendedorId,
                EmisorId = miId,
                Contenido = contenido.Trim(),
                ProductoId = productoId,
                PedidoId = pedidoId
            };

            _context.Mensajes.Add(mensaje);
            await _context.SaveChangesAsync();

            string? nombreProducto = null;
            if (productoId.HasValue)
            {
                var producto = await _context.Productos.FindAsync(productoId.Value);
                nombreProducto = producto?.Nombre;
            }

            var payload = new
            {
                emisorId = mensaje.EmisorId,
                contenido = mensaje.Contenido,
                fecha = mensaje.FechaEnvio.ToString("dd/MM HH:mm"),
                productoNombre = nombreProducto,
                pedidoId = mensaje.PedidoId
            };

            // Mensaje en vivo a quien tenga la conversación abierta
            await Clients.Group(NombreGrupoConversacion(clienteId, vendedorId))
                .SendAsync("RecibirMensaje", payload);

            // Notificación del badge para el otro usuario, tenga o no la conversación abierta
            await Clients.Group($"usuario-{otroUsuarioId}")
                .SendAsync("NuevoMensajeNotificacion");
        }

        private async Task<(int clienteId, int vendedorId)?> ResolverParticipantes(int otroUsuarioId)
        {
            var miId = ObtenerUsuarioId();
            var yo = await _context.Usuarios.FindAsync(miId);
            var otro = await _context.Usuarios.FindAsync(otroUsuarioId);

            if (yo == null || otro == null) return null;

            if (yo.RolId == 3 && otro.RolId == 2) return (yo.Id, otro.Id);
            if (yo.RolId == 2 && otro.RolId == 3) return (otro.Id, yo.Id);

            return null;
        }
    }
}
