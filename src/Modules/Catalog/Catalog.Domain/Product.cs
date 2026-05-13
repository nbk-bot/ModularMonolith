using BuildingBlocks.Domain;

namespace Catalog.Domain;

public sealed class Product : AggregateRoot<Guid>
{
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public int Stock { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Product() { }

    public static Product Create(string name, decimal price, int stock, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name must not be empty", nameof(name));
        if (price <= 0) throw new ArgumentException("Price must be > 0", nameof(price));
        if (stock < 0) throw new ArgumentException("Stock must be >= 0", nameof(stock));

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Price = price,
            Stock = stock,
            Description = description,
            CreatedAt = DateTime.UtcNow,
        };
        product.Raise(new ProductCreated(product.Id, product.Name, product.Price));
        return product;
    }

    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice <= 0) throw new ArgumentException("Price must be > 0");
        Price = newPrice;
        Raise(new ProductPriceChanged(Id, newPrice));
    }

    public void Restock(int amount)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be > 0");
        Stock += amount;
    }
}

public sealed record ProductCreated(Guid ProductId, string Name, decimal Price) : IDomainEvent;
public sealed record ProductPriceChanged(Guid ProductId, decimal NewPrice) : IDomainEvent;
