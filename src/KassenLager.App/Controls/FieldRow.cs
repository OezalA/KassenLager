namespace KassenLager.App.Controls;

/// <summary>Read-only "caption over value" pair of detail pages; hidden while the value is empty.</summary>
public sealed class FieldRow : ContentView
{
    public static readonly BindableProperty CaptionProperty = BindableProperty.Create(
        nameof(Caption), typeof(string), typeof(FieldRow), string.Empty,
        propertyChanged: (b, _, value) => ((FieldRow)b)._caption.Text = (string)value);

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(string), typeof(FieldRow), null,
        propertyChanged: (b, _, value) =>
        {
            var row = (FieldRow)b;
            row._value.Text = (string?)value;
            row.IsVisible = !string.IsNullOrEmpty((string?)value);
        });

    private readonly Label _caption = new();
    private readonly Label _value = new();

    public FieldRow()
    {
        _caption.SetDynamicResource(StyleProperty, "FormLabel");
        _value.SetDynamicResource(StyleProperty, "FieldValue");
        IsVisible = false;
        Content = new VerticalStackLayout { Children = { _caption, _value } };
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public string? Value
    {
        get => (string?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }
}
