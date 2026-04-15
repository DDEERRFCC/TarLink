using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

public class CommitteeCohortsModel : CommitteePageModelBase
{
    private static readonly string[] AllowedFaculties =
    {
        "Faculty of Computing and Information Technology",
        "Faculty of Engineering and Technology",
        "Faculty of Business and Finance",
        "Faculty of Accountancy, Finance and Business",
        "Faculty of Social Science and Humanities",
        "Faculty of Built Environment"
    };

    private static readonly string[] AllowedCampuses =
    {
        "Kuala Lumpur Main Campus",
        "Penang Branch Campus",
        "Perak Branch Campus",
        "Johor Branch Campus",
        "Sabah Branch Campus",
        "Sarawak Branch Campus"
    };

    private readonly ApplicationDbContext _db;

    public CommitteeCohortsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<Cohort> Cohorts { get; private set; } = new();
    public int TotalCohorts { get; private set; }
    public int ActiveCohorts { get; private set; }

    public IReadOnlyList<SelectListItem> FacultyOptions { get; } = AllowedFaculties
        .Select(x => new SelectListItem(x, x))
        .ToList();

    public IReadOnlyList<SelectListItem> CampusOptions { get; } = AllowedCampuses
        .Select(x => new SelectListItem(x, x))
        .ToList();

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public byte? LevelFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? ActiveFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? CampusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FacultyFilter { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        LoadCohorts();
        return Page();
    }

    public IActionResult OnPostDelete(int id)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var cohort = _db.Cohorts.FirstOrDefault(c => c.cohort_id == id);
        if (cohort == null)
        {
            ErrorMessage = "Selected cohort was not found.";
            return RedirectToPage();
        }

        var hasStudents = _db.StudentApplications.AsNoTracking().Any(s => s.cohortId == id);
        if (hasStudents)
        {
            ErrorMessage = $"Cohort {id} cannot be deleted because it is linked to student records.";
            return RedirectToPage();
        }

        _db.Cohorts.Remove(cohort);
        _db.SaveChanges();

        StatusMessage = $"Cohort {id} deleted successfully.";
        return RedirectToPage();
    }

    public string GetLevelLabel(byte? level)
    {
        return level switch
        {
            1 => "Diploma",
            2 => "Degree",
            _ => "-"
        };
    }

    public bool HasActiveFilters()
    {
        return !string.IsNullOrWhiteSpace(SearchTerm)
            || LevelFilter.HasValue
            || ActiveFilter.HasValue
            || !string.IsNullOrWhiteSpace(CampusFilter)
            || !string.IsNullOrWhiteSpace(FacultyFilter);
    }

    private void LoadCohorts()
    {
        TotalCohorts = _db.Cohorts.Count();
        ActiveCohorts = _db.Cohorts.Count(c => c.isActive);

        var query = _db.Cohorts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            var term = SearchTerm.Trim();
            query = query.Where(c =>
                (c.description != null && EF.Functions.Like(c.description, $"%{term}%")) ||
                (c.personInCharge != null && EF.Functions.Like(c.personInCharge, $"%{term}%")) ||
                (c.pidEmail != null && EF.Functions.Like(c.pidEmail, $"%{term}%")) ||
                c.cohort_id.ToString().Contains(term));
        }

        if (LevelFilter.HasValue)
        {
            query = query.Where(c => c.level == LevelFilter.Value);
        }

        if (ActiveFilter.HasValue)
        {
            query = query.Where(c => c.isActive == ActiveFilter.Value);
        }

        if (!string.IsNullOrWhiteSpace(CampusFilter))
        {
            var campus = CampusFilter.Trim();
            query = query.Where(c => c.campus == campus);
        }

        if (!string.IsNullOrWhiteSpace(FacultyFilter))
        {
            var faculty = FacultyFilter.Trim();
            query = query.Where(c => c.faculty == faculty);
        }

        Cohorts = query
            .OrderByDescending(c => c.startDate)
            .ThenByDescending(c => c.cohort_id)
            .ToList();
    }
}
