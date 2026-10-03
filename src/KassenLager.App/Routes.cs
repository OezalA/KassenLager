namespace KassenLager.App;

public static class Routes
{
    // Tabs
    public const string Overview = "overview";
    public const string Search = "search";
    public const string Booking = "booking";
    public const string Inventory = "inventory";
    public const string More = "more";

    // Pushed pages
    public const string Settings = "settings";
    public const string Customers = "customers";
    public const string CustomerEdit = "customer-edit";
    public const string Categories = "categories";
    public const string CategoryEdit = "category-edit";
    public const string Units = "units";
    public const string Articles = "articles";
    public const string ArticleEdit = "article-edit";

    /// <summary>Query parameter carrying the id of the entity to edit; absent for "new".</summary>
    public const string IdParameter = "id";
}
