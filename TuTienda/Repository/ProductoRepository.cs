using TuTienda.Models.Entities;
using Microsoft.Data.SqlClient;

namespace TuTienda.Repository
{
    public class ProductoRepository
    {
        private readonly string _cadenaConexion;

        public ProductoRepository(IConfiguration configuracion)
        {
            _cadenaConexion = configuracion.GetConnectionString("DefaultConnection");
        }

        public async Task AgregarProducto(Producto producto)
        {
            var sql = @"INSERT INTO Productos (Nombre, Descripcion, Precio, Stock, ImagenUrl, CategoriaId, VendedorId, Activo, FechaCreacion) 
                        VALUES (@Nombre, @Descripcion, @Precio, @Stock, @ImagenUrl, @CategoriaId, @VendedorId, @Activo, GETDATE())";

            using (var con = new SqlConnection(_cadenaConexion))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@Nombre", producto.Nombre);
                cmd.Parameters.AddWithValue("@Descripcion", (object?)producto.Descripcion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Precio", producto.Precio);
                cmd.Parameters.AddWithValue("@Stock", producto.Stock);
                cmd.Parameters.AddWithValue("@ImagenUrl", (object?)producto.ImagenUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CategoriaId", producto.CategoriaId);
                cmd.Parameters.AddWithValue("@VendedorId", producto.VendedorId);
                cmd.Parameters.AddWithValue("@Activo", producto.Activo);

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<(List<Producto> productos, int totalRegistros)> ObtenerProductosPaginados(
            string nombre,
            decimal precioMin,
            int paginaActual,
            int elementosPorPagina
        )
        {
            var lista = new List<Producto>();
            int total = 0;
            var sql = "dbo.sp_FiltrarProductosPaginados";

            using (var con = new SqlConnection(_cadenaConexion))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@nombre", nombre ?? "");
                cmd.Parameters.AddWithValue("@precioMin", precioMin);
                cmd.Parameters.AddWithValue("@paginaActual", paginaActual);
                cmd.Parameters.AddWithValue("@elementosPorPagina", elementosPorPagina);

                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        lista.Add(new Producto
                        {
                            Id = reader.GetInt32(0),
                            Nombre = reader.GetString(1),
                            Descripcion = reader.IsDBNull(2) ? null : reader.GetString(2),
                            Precio = reader.GetDecimal(3),
                            Stock = reader.GetInt32(4),
                            ImagenUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
                            CategoriaId = reader.GetInt32(6),
                            VendedorId = reader.GetInt32(7),
                            Activo = reader.GetBoolean(8)
                        });
                        total = reader.GetInt32(9);
                    }
                    return (lista, total);
                }
            }
        }
    }
}
