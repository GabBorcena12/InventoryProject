namespace Inventory.API.Dtos
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string ProductName { get; set; }
        public int Volume { get; set; }
        public string UnitOfMeasure { get; set; }
    }
}