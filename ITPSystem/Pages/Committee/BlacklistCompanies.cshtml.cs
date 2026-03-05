using Microsoft.AspNetCore.Mvc;

public class CommitteeBlacklistCompaniesModel : CommitteePageModelBase
{
    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        return Page();
    }
}
