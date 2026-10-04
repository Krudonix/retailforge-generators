using System.ComponentModel.DataAnnotations;
using RetailForge.Generators.Abstractions;

namespace RetailForge.Generators.TestConsole;

[GenerateSQL("products")]
public class Product
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public int Stock_Count { get; set; }
    public decimal Price { get; set; }
}
