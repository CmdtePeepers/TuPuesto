using Microsoft.Data.SqlClient;
using System.Data;
using TuTienda.Models.ViewModels;

namespace TuTienda.Repository
{
    public class ReporteRepository
    {
        private readonly string _cadenaConexion;

        public ReporteRepository(IConfiguration configuracion)
        {
            _cadenaConexion = configuracion.GetConnectionString("DefaultConnection");
        }

        public async Task<List<VentaPorDiaVM>> VentasPorPeriodo(DateTime fechaInicio, DateTime fechaFin)
        {
            var lista = new List<VentaPorDiaVM>();

            using var con = new SqlConnection(_cadenaConexion);
            using var cmd = new SqlCommand("dbo.sp_ReporteVentasPorPeriodo", con) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@fechaInicio", fechaInicio.Date);
            cmd.Parameters.AddWithValue("@fechaFin", fechaFin.Date);

            await con.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new VentaPorDiaVM
                {
                    Fecha = reader.GetDateTime(0),
                    CantidadPedidos = reader.GetInt32(1),
                    Total = reader.GetDecimal(2)
                });
            }
            return lista;
        }

        public async Task<List<ProductoMasVendidoVM>> ProductosMasVendidos(DateTime? fechaInicio, DateTime? fechaFin, int topN = 10)
        {
            var lista = new List<ProductoMasVendidoVM>();

            using var con = new SqlConnection(_cadenaConexion);
            using var cmd = new SqlCommand("dbo.sp_ReporteProductosMasVendidos", con) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@fechaInicio", (object?)fechaInicio?.Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@fechaFin", (object?)fechaFin?.Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@topN", topN);

            await con.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new ProductoMasVendidoVM
                {
                    ProductoId = reader.GetInt32(0),
                    Nombre = reader.GetString(1),
                    CantidadVendida = reader.GetInt32(2),
                    Ingresos = reader.GetDecimal(3)
                });
            }
            return lista;
        }

        public async Task<List<IngresoPorVendedorVM>> IngresosPorVendedor(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var lista = new List<IngresoPorVendedorVM>();

            using var con = new SqlConnection(_cadenaConexion);
            using var cmd = new SqlCommand("dbo.sp_ReporteIngresosPorVendedor", con) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@fechaInicio", (object?)fechaInicio?.Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@fechaFin", (object?)fechaFin?.Date ?? DBNull.Value);

            await con.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new IngresoPorVendedorVM
                {
                    VendedorId = reader.GetInt32(0),
                    NombreVendedor = reader.GetString(1),
                    CantidadPedidos = reader.GetInt32(2),
                    Total = reader.GetDecimal(3)
                });
            }
            return lista;
        }

        public async Task<List<VentaPorMesVM>> ComparacionMensual(int anio)
        {
            var lista = new List<VentaPorMesVM>();

            using var con = new SqlConnection(_cadenaConexion);
            using var cmd = new SqlCommand("dbo.sp_ReporteComparacionMensual", con) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@anio", anio);

            await con.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new VentaPorMesVM
                {
                    Mes = reader.GetInt32(0),
                    CantidadPedidos = reader.GetInt32(1),
                    Total = reader.GetDecimal(2)
                });
            }
            return lista;
        }
    }
}
