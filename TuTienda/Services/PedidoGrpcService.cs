using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using TuTienda.Data;
using TuTienda.Grpc;

namespace TuTienda.Services
{
    public class PedidoGrpcService : PedidoService.PedidoServiceBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PedidoGrpcService> _logger;

        public PedidoGrpcService(AppDbContext context, ILogger<PedidoGrpcService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public override async Task<PedidoEstadoResponse> ConsultarEstadoPedido(PedidoRequest request, ServerCallContext context)
        {
            try
            {
                var pedido = await _context.Pedidos
                    .Include(p => p.Vendedor)
                    .FirstOrDefaultAsync(p => p.Id == request.Id);

                if (pedido == null)
                {
                    return new PedidoEstadoResponse
                    {
                        Encontrado = false,
                        Mensaje = $"Pedido con Id {request.Id} no encontrado"
                    };
                }

                return new PedidoEstadoResponse
                {
                    Id = pedido.Id,
                    Estado = pedido.Estado.ToString(),
                    Total = (double)pedido.Total,
                    FechaPedido = pedido.FechaPedido.ToString("yyyy-MM-dd HH:mm"),
                    Vendedor = pedido.Vendedor?.NombreTienda ?? pedido.Vendedor?.Nombres ?? "",
                    GrupoCompraId = pedido.GrupoCompraId.ToString(),
                    Encontrado = true,
                    Mensaje = "Pedido encontrado correctamente"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar el pedido con Id {id}", request.Id);
                throw new RpcException(new Status(StatusCode.Internal, "Error interno al consultar el pedido"));
            }
        }

        public override async Task<ListarPedidosResponse> ListarPedidosPorCliente(ClienteRequest request, ServerCallContext context)
        {
            try
            {
                var pedidos = await _context.Pedidos
                    .Include(p => p.Vendedor)
                    .Where(p => p.ClienteId == request.ClienteId)
                    .OrderByDescending(p => p.FechaPedido)
                    .ToListAsync();

                var response = new ListarPedidosResponse
                {
                    TotalRegistros = pedidos.Count
                };

                foreach (var pedido in pedidos)
                {
                    response.Pedidos.Add(new PedidoEstadoResponse
                    {
                        Id = pedido.Id,
                        Estado = pedido.Estado.ToString(),
                        Total = (double)pedido.Total,
                        FechaPedido = pedido.FechaPedido.ToString("yyyy-MM-dd HH:mm"),
                        Vendedor = pedido.Vendedor?.NombreTienda ?? pedido.Vendedor?.Nombres ?? "",
                        GrupoCompraId = pedido.GrupoCompraId.ToString(),
                        Encontrado = true
                    });
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar pedidos del cliente {id}", request.ClienteId);
                throw new RpcException(new Status(StatusCode.Internal, "Error interno al listar pedidos"));
            }
        }
    }
}
