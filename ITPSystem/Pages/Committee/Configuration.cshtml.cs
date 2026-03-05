using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class CommitteeConfigurationModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeConfigurationModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<Cohort> Cohorts { get; private set; } = new();

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        Cohorts = _db.Cohorts.AsNoTracking()
            .OrderByDescending(c => c.startDate)
            .ToList();

        return Page();
    }
}
