using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class CommitteeDashboardModel : PageModel
{
    public IActionResult OnGet()
    {
        var role = HttpContext.Session.GetString("UserRole");
        if (!string.Equals(role, "committee", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToPage("/Login/Login");
        }

        return Page();
    }
}
