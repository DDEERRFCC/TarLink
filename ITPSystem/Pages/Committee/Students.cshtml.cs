using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;

public class CommitteeStudentsModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeStudentsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<StudentApplication> Students { get; private set; } = new();
    public List<SelectListItem> CohortOptions { get; private set; } = new();
    public List<SelectListItem> ProgrammeOptions { get; private set; } = new();
    public int TotalStudents { get; private set; }
    public int FilteredStudents { get; private set; }

    [BindProperty(SupportsGet = true)]
    public string Search { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string StatusFilter { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public int? CohortFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string ProgrammeFilter { get; set; } = string.Empty;
    
    [TempData]
    public string? StatusMessage { get; set; }

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        TotalStudents = _db.StudentApplications.Count();

        var query = _db.StudentApplications.AsNoTracking()
            .Include(s => s.Cohort)
            .AsQueryable();

        var keyword = (Search ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(s =>
                (s.studentID ?? string.Empty).Contains(keyword) ||
                (s.studentName ?? string.Empty).Contains(keyword) ||
                (s.studentEmail ?? string.Empty).Contains(keyword) ||
                (s.programme ?? string.Empty).Contains(keyword) ||
                (s.comName ?? string.Empty).Contains(keyword));
        }

        var normalizedStatus = (StatusFilter ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            query = query.Where(s => (s.applyStatus ?? string.Empty).ToLower() == normalizedStatus);
        }

        if (CohortFilter.HasValue)
        {
            query = query.Where(s => s.cohortId == CohortFilter.Value);
        }

        var normalizedProgramme = (ProgrammeFilter ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(normalizedProgramme))
        {
            query = query.Where(s => (s.programme ?? string.Empty) == normalizedProgramme);
        }

        Students = query
            .OrderBy(s => s.studentName)
            .ToList();
        FilteredStudents = Students.Count;

        CohortOptions = _db.Cohorts.AsNoTracking()
            .OrderByDescending(c => c.isActive)
            .ThenBy(c => c.description)
            .Select(c => new SelectListItem
            {
                Value = c.cohort_id.ToString(),
                Text = string.IsNullOrWhiteSpace(c.description)
                    ? $"Cohort {c.cohort_id}"
                    : c.description
            })
            .ToList();

        ProgrammeOptions = _db.StudentApplications.AsNoTracking()
            .Select(s => s.programme)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .Distinct()
            .OrderBy(p => p)
            .Select(p => new SelectListItem
            {
                Value = p,
                Text = p
            })
            .ToList();

        return Page();
    }
}
