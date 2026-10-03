using KassenLager.App.ViewModels.BranchIssues;

namespace KassenLager.App.Views.BranchIssues;

public partial class BranchIssueListPage : ContentPageBase
{
    public BranchIssueListPage(BranchIssueListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
