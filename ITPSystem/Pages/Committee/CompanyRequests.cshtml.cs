using Microsoft.AspNetCore.Mvc;

public class CommitteeCompanyRequestsModel : CommitteePageModelBase
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
