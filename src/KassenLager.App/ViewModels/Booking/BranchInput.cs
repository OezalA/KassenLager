using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.Core.Domain;
using KassenLager.Core.Text;

namespace KassenLager.App.ViewModels.Booking;

/// <summary>Branch (Filiale) text field with suggestions from the customer's earlier bookings.</summary>
public sealed partial class BranchInput : ObservableObject
{
    private const int MaxSuggestions = 6;

    private IReadOnlyList<string> _known = [];

    public int MaxLength => Movement.BranchMaxLength;

    [ObservableProperty]
    public partial string? Text { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string>? Suggestions { get; set; }

    public void SetKnownBranches(IReadOnlyList<string> names)
    {
        _known = names;
        UpdateSuggestions();
    }

    partial void OnTextChanged(string? value) => UpdateSuggestions();

    [RelayCommand]
    private void Choose(string name) => Text = name;

    private void UpdateSuggestions() =>
        Suggestions = [.. _known
            .Where(name => TextSearch.Matches(Text, name) && !TextKey.EqualsIgnoreCase(name, Text))
            .Take(MaxSuggestions)];
}
