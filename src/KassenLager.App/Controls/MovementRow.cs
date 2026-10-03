using System.Globalization;
using System.Windows.Input;
using KassenLager.Core.Services;

namespace KassenLager.App.Controls;

/// <summary>
/// List row of a movement: type, article, customer/branch/reference and state change on the left;
/// date, quantity change and a "storniert" badge on the right.
/// </summary>
public sealed class MovementRow : ContentView
{
    public static readonly BindableProperty ItemProperty = BindableProperty.Create(
        nameof(Item), typeof(MovementListItem), typeof(MovementRow), null,
        propertyChanged: (b, _, value) => ((MovementRow)b).Show((MovementListItem?)value));

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(MovementRow), null,
        propertyChanged: (b, _, value) => ((MovementRow)b)._tap.Command = (ICommand?)value);

    private readonly Label _type = new() { FontSize = 16 };
    private readonly Label _article = new();
    private readonly Label _details = new() { FontSize = 13 };
    private readonly Label _state = new() { FontSize = 13 };
    private readonly Label _time = new() { HorizontalOptions = LayoutOptions.End };
    private readonly Label _quantity = new() { FontFamily = "OpenSansSemibold", HorizontalOptions = LayoutOptions.End };
    private readonly Badge _reversed = new() { Text = "storniert", HorizontalOptions = LayoutOptions.End };
    private readonly TapGestureRecognizer _tap = new();

    public MovementRow()
    {
        _type.SetDynamicResource(StyleProperty, "ItemTitle");
        _article.SetDynamicResource(StyleProperty, "ItemSubtitle");
        _details.SetDynamicResource(StyleProperty, "ItemSubtitle");
        _state.SetDynamicResource(StyleProperty, "ItemSubtitle");
        _time.SetDynamicResource(StyleProperty, "Hint");

        var row = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        row.SetDynamicResource(StyleProperty, "ListRow");
        row.Add(new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 2, Children = { _type, _article, _details, _state } }, 0);
        row.Add(new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 4, Children = { _time, _quantity, _reversed } }, 1);
        row.GestureRecognizers.Add(_tap);

        var divider = new BoxView();
        divider.SetDynamicResource(StyleProperty, "Divider");

        Content = new VerticalStackLayout { Children = { row, divider } };
    }

    public MovementListItem? Item
    {
        get => (MovementListItem?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    private void Show(MovementListItem? item)
    {
        _tap.CommandParameter = item;
        _type.Text = item?.TypeName;
        _article.Text = item?.ArticleText;
        _details.Text = item?.DetailText;
        _state.Text = item?.StateText;
        _state.IsVisible = item?.StateText is not null;
        _time.Text = item?.LocalTime.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);
        _quantity.Text = item?.QuantityText;
        _reversed.IsVisible = item?.IsReversed == true;
    }
}
