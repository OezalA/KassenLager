using KassenLager.App.ViewModels.BranchIssues;

namespace KassenLager.App.Views.BranchIssues;

public partial class BranchIssueDetailPage : ContentPageBase
{
    public BranchIssueDetailPage(BranchIssueDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
