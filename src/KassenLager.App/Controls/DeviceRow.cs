using System.Windows.Input;
using KassenLager.Core.Services;

namespace KassenLager.App.Controls;

/// <summary>List row of a device: serial number, article, customer and a state badge. Tapping runs <see cref="Command"/> with the device.</summary>
public sealed class DeviceRow : ContentView
{
    public static readonly BindableProperty ItemProperty = BindableProperty.Create(
        nameof(Item), typeof(DeviceListItem), typeof(DeviceRow), null,
        propertyChanged: (b, _, value) => ((DeviceRow)b).Show((DeviceListItem?)value));

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(DeviceRow), null,
        propertyChanged: (b, _, value) => ((DeviceRow)b)._tap.Command = (ICommand?)value);

    public static readonly BindableProperty ShowCustomerProperty = BindableProperty.Create(
        nameof(ShowCustomer), typeof(bool), typeof(DeviceRow), true,
        propertyChanged: (b, _, _) => ((DeviceRow)b).Show(((DeviceRow)b).Item));

    private readonly Label _serial = new();
    private readonly Label _article = new();
    private readonly Label _customer = new() { FontSize = 13 };
    private readonly Badge _state = new() { HorizontalOptions = LayoutOptions.End };
    private readonly TapGestureRecognizer _tap = new();

    public DeviceRow()
    {
        _serial.SetDynamicResource(StyleProperty, "ItemTitle");
        _article.SetDynamicResource(StyleProperty, "ItemSubtitle");
        _customer.SetDynamicResource(StyleProperty, "ItemSubtitle");

        var row = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        row.SetDynamicResource(StyleProperty, "ListRow");
        row.Add(new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 2, Children = { _serial, _article, _customer } }, 0);
        row.Add(_state, 1);
        row.GestureRecognizers.Add(_tap);

        var divider = new BoxView();
        divider.SetDynamicResource(StyleProperty, "Divider");

        Content = new VerticalStackLayout { Children = { row, divider } };
    }

    public DeviceListItem? Item
    {
        get => (DeviceListItem?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public bool ShowCustomer
    {
        get => (bool)GetValue(ShowCustomerProperty);
        set => SetValue(ShowCustomerProperty, value);
    }

    private void Show(DeviceListItem? item)
    {
        _tap.CommandParameter = item;
        _serial.Text = item?.SerialNumber;
        _article.Text = item?.ArticleText;
        _customer.Text = item?.CustomerName;
        _customer.IsVisible = ShowCustomer;
        _state.Text = item?.StateName;
        _state.IsDanger = item?.IsDefective == true;
    }
}
