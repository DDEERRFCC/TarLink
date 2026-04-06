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
    public int TotalCompanies { get; private set; }
    public int FilteredCompanies { get; private set; }
    public int ActiveCompanies { get; private set; }
    public int PendingCompanies { get; private set; }
    public int VisibleCompanies { get; private set; }
    public int SuspendedCompanies { get; private set; }
    public int DiplomaCompanies { get; private set; }
    public int DegreeCompanies { get; private set; }

    [BindProperty(SupportsGet = true)]
    public string Search { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string StatusFilter { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string VisibilityFilter { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string VacancyLevelFilter { get; set; } = string.Empty;

    [TempData]
    public string? StatusMessage { get; set; }

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        LoadCompanies();

        return Page();
    }

    private void LoadCompanies()
    {
        TotalCompanies = _db.Companies.Count();

        var query = _db.Companies
            .AsNoTracking()
            .Include(c => c.Branches)
            .AsQueryable();

        var keyword = (Search ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(c =>
                (c.name ?? string.Empty).Contains(keyword) ||
                (c.regNo ?? string.Empty).Contains(keyword) ||
                (c.website ?? string.Empty).Contains(keyword) ||
                c.Branches.Any(a =>
                    (a.address_line ?? string.Empty).Contains(keyword) ||
                    (a.city ?? string.Empty).Contains(keyword) ||
                    (a.state ?? string.Empty).Contains(keyword) ||
                    (a.postcode ?? string.Empty).Contains(keyword) ||
                    (a.country ?? string.Empty).Contains(keyword)));
        }

        if (byte.TryParse((StatusFilter ?? string.Empty).Trim(), out var statusByte))
        {
            query = query.Where(c => c.Branches.Any(b => (b.status ?? 0) == statusByte));
        }

        if (byte.TryParse((VisibilityFilter ?? string.Empty).Trim(), out var visibilityByte))
        {
            query = query.Where(c => c.Branches.Any(b => (b.visibility ?? 1) == visibilityByte));
        }

        var vacancyLevel = (VacancyLevelFilter ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(vacancyLevel))
        {
            var v = vacancyLevel.ToLower();
            query = query.Where(c => c.Branches.Any(b => (b.vacancyLevel ?? string.Empty).ToLower() == v));
        }

        Companies = query
            .OrderBy(c => c.company_id)
            .ToList();
        FilteredCompanies = Companies.Count;
        PendingCompanies = Companies.Count(c => c.Branches.Any(b => (b.status ?? 0) == 0));
        ActiveCompanies = Companies.Count(c => c.Branches.Any(b => (b.status ?? 0) == 1));
        VisibleCompanies = Companies.Count(c => c.Branches.Any(b => (b.visibility ?? 1) == 1));
        SuspendedCompanies = Companies.Count(c => c.Branches.Any(b => (b.status ?? 0) == 3));
        DiplomaCompanies = Companies.Count(c => c.Branches.Any(b => string.Equals(b.vacancyLevel, "Diploma", StringComparison.OrdinalIgnoreCase)));
        DegreeCompanies = Companies.Count(c => c.Branches.Any(b => string.Equals(b.vacancyLevel, "Degree", StringComparison.OrdinalIgnoreCase)));
    }
}
