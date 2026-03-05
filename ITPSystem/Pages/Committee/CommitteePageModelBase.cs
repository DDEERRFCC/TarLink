using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public abstract class CommitteePageModelBase : PageModel
{
    protected bool IsCommittee()
    {
        var role = HttpContext.Session.GetString("UserRole");
        return string.Equals(role, "committee", StringComparison.OrdinalIgnoreCase);
    }

    protected IActionResult RedirectIfNotCommittee()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        return Page();
    }
}
