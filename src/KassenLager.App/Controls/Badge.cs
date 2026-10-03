namespace KassenLager.App.Controls;

/// <summary>Small rounded status label; red when <see cref="IsDanger"/> is set.</summary>
public sealed class Badge : Label
{
    public static readonly BindableProperty IsDangerProperty = BindableProperty.Create(
        nameof(IsDanger), typeof(bool), typeof(Badge), false,
        propertyChanged: (b, _, value) => ((Badge)b).ApplyStyle((bool)value));

    public Badge() => ApplyStyle(false);

    public bool IsDanger
    {
        get => (bool)GetValue(IsDangerProperty);
        set => SetValue(IsDangerProperty, value);
    }

    private void ApplyStyle(bool isDanger) => SetDynamicResource(StyleProperty, isDanger ? "DangerBadge" : "Badge");
}
