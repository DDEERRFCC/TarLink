using Microsoft.AspNetCore.Mvc;

public class CommitteeImportStudentsModel : CommitteePageModelBase
{
    [TempData]
    public string? Message { get; set; }

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        return Page();
    }

    public IActionResult OnPost()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        Message = "Import placeholder only. CSV import logic is not implemented yet.";
        return RedirectToPage();
    }
}
