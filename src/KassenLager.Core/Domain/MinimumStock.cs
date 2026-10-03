namespace KassenLager.Core.Domain;

/// <summary>Optional minimum stock (Mindestbestand) per article and customer.</summary>
public class MinimumStock
{
    public int ArticleId { get; set; }

    public Article? Article { get; set; }

    public int CustomerId { get; set; }

    public Customer? Customer { get; set; }

    /// <summary>For serial-tracked articles compared with the available devices, otherwise with the quantity.</summary>
    public int Quantity { get; set; }
}
