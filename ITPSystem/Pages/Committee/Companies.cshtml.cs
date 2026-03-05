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

        var query = _db.Companies.AsNoTracking().AsQueryable();

        var keyword = (Search ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(c =>
                (c.name ?? string.Empty).Contains(keyword) ||
                (c.address1 ?? string.Empty).Contains(keyword) ||
                (c.address2 ?? string.Empty).Contains(keyword) ||
                (c.address3 ?? string.Empty).Contains(keyword) ||
                (c.regNo ?? string.Empty).Contains(keyword) ||
                (c.website ?? string.Empty).Contains(keyword));
        }

        if (byte.TryParse((StatusFilter ?? string.Empty).Trim(), out var statusByte))
        {
            query = query.Where(c => c.status == statusByte);
        }

        if (byte.TryParse((VisibilityFilter ?? string.Empty).Trim(), out var visibilityByte))
        {
            query = query.Where(c => c.visibility == visibilityByte);
        }

        var vacancyLevel = (VacancyLevelFilter ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(vacancyLevel))
        {
            var v = vacancyLevel.ToLower();
            query = query.Where(c => (c.vacancyLevel ?? string.Empty).ToLower() == v);
        }

        Companies = query
            .OrderBy(c => c.company_id)
            .ToList();
        FilteredCompanies = Companies.Count;
    }
}
