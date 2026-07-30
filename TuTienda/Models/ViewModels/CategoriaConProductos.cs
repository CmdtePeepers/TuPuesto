using TuTienda.Models.Entities;

namespace TuTienda.Models.ViewModels
{
    public class CategoriaConProductos
    {
        public Categoria Categoria { get; set; } = null!;
        public List<Producto> Productos { get; set; } = new();
    }
}
