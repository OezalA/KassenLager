using KassenLager.App.ViewModels;

namespace KassenLager.App.Views;

public partial class BookingPage : ContentPage
{
    public BookingPage(BookingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
