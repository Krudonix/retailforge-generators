using RetailForge.Generators.Abstractions;
using RetailForge.Generators.TestConsole;

[assembly: PropertyResolverTarget(typeof(Product))]

// --- PropertyResolver ---
Console.WriteLine("=== PropertyResolver ===");
var product = new Product
{
    Id = "prod-001",
    Name = "Widget",
    Barcode = "1234567890",
    Stock_Count = 42,
    Price = 9.99m,
};

string[] propertyNames = ["id", "name", "barcode", "stock_count", "stockcount", "price", "unknown"];

foreach (var name in propertyNames)
{
    if (product.ContainsProperty(name))
        Console.WriteLine($"  {name,-15} = {product.GetPropertyValue(name)}");
    else
        Console.WriteLine($"  {name,-15} (not found)");
}

// --- SQL Queries ---
Console.WriteLine();
Console.WriteLine("=== SQL Queries ===");
Console.WriteLine();
Console.WriteLine("[Insert]");
Console.WriteLine(ProductQueries.Insert.Sql);
Console.WriteLine();
Console.WriteLine("[Update]");
Console.WriteLine(ProductQueries.Update.Sql);
Console.WriteLine();
Console.WriteLine("[Delete]");
Console.WriteLine(ProductQueries.Delete.Sql);
Console.WriteLine();
Console.WriteLine("[Select.All]");
Console.WriteLine(ProductQueries.Select.All);
Console.WriteLine();
Console.WriteLine("[Select.ById]");
Console.WriteLine(ProductQueries.Select.ById);

// --- Poco + resolver ---
Console.WriteLine();
Console.WriteLine("=== Generated POCO (ItemQuery.Result) ===");
var result = new ItemQuery.Result { Sid = "123", Alu = "ALU-001", Vendor = "Acme" };
Console.WriteLine($"  ContainsProperty(\"sid\")     = {result.ContainsProperty("sid")}");
Console.WriteLine($"  ContainsProperty(\"unknown\") = {result.ContainsProperty("unknown")}");
Console.WriteLine($"  GetPropertyValue(\"sid\")     = {result.GetPropertyValue("sid")}");
Console.WriteLine($"  GetPropertyValue(\"alu\")     = {result.GetPropertyValue("alu")}");
Console.WriteLine($"  GetPropertyValue(\"vendor\")  = {result.GetPropertyValue("vendor")}");
