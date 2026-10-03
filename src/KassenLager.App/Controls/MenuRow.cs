using System.Windows.Input;

namespace KassenLager.App.Controls;

/// <summary>Full-width tappable navigation row: icon, title, optional detail line, chevron.</summary>
public sealed class MenuRow : ContentView
{
    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon), typeof(string), typeof(MenuRow), string.Empty,
        propertyChanged: (b, _, value) => ((MenuRow)b)._icon.Text = (string)value);

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(MenuRow), string.Empty,
        propertyChanged: (b, _, value) => ((MenuRow)b)._text.Text = (string)value);

    public static readonly BindableProperty DetailProperty = BindableProperty.Create(
        nameof(Detail), typeof(string), typeof(MenuRow), null,
        propertyChanged: (b, _, value) =>
        {
            var row = (MenuRow)b;
            row._detail.Text = (string?)value;
            row._detail.IsVisible = !string.IsNullOrEmpty((string?)value);
        });

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(MenuRow), null,
        propertyChanged: (b, _, value) => ((MenuRow)b)._tap.Command = (ICommand?)value);

    private readonly Label _icon = new();
    private readonly Label _text = new();
    private readonly Label _detail = new() { IsVisible = false };
    private readonly TapGestureRecognizer _tap = new();

    public MenuRow()
    {
        _icon.SetDynamicResource(StyleProperty, "Icon");
        _text.SetDynamicResource(StyleProperty, "ItemTitle");
        _detail.SetDynamicResource(StyleProperty, "ItemSubtitle");

        var chevron = new Label { Text = Icons.ChevronRight };
        chevron.SetDynamicResource(StyleProperty, "Chevron");

        var row = new Grid
        {
            ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) },
        };
        row.SetDynamicResource(StyleProperty, "ListRow");
        row.Add(_icon, 0);
        row.Add(new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Children = { _text, _detail } }, 1);
        row.Add(chevron, 2);
        row.GestureRecognizers.Add(_tap);

        var divider = new BoxView();
        divider.SetDynamicResource(StyleProperty, "Divider");

        Content = new VerticalStackLayout { Children = { row, divider } };
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Detail
    {
        get => (string?)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }
}
