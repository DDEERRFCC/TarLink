using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class CommitteeCompaniesModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeCompaniesModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<Company> Companies { get; private set; } = new();

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        Companies = _db.Companies.AsNoTracking()
            .OrderBy(c => c.name)
            .ToList();

        return Page();
    }
}
