using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
public class CommitteeStudentAccessModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeStudentAccessModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<StudentAccessItem> StudentAccesses { get; private set; } = new();
    public List<StudentAccessGroup> StudentAccessGroups { get; private set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        LoadData();

        return Page();
    }

    private void LoadData()
    {
        var cohorts = _db.Cohorts.AsNoTracking()
            .ToDictionary(c => c.cohort_id, c => c.description ?? $"Cohort {c.cohort_id}");

        var students = _db.StudentApplications.AsNoTracking()
            .Include(s => s.Cohort)
            .ToDictionary(s => s.application_id);
        StudentAccesses = _db.SysUsers.AsNoTracking()
            .Where(u => u.role == "student" && u.is_active)
            .OrderBy(u => u.username)
            .ToList()
            .Select(u =>
            {
                StudentApplication? app = null;
                if (u.application_id.HasValue)
                {
                    students.TryGetValue(u.application_id.Value, out app);
                }
                if (app == null && !string.IsNullOrWhiteSpace(u.email))
                {
                    app = _db.StudentApplications.AsNoTracking()
                        .FirstOrDefault(s => s.studentEmail == u.email);
                }
                if (app == null && !string.IsNullOrWhiteSpace(u.ic_number))
                {
                    app = _db.StudentApplications.AsNoTracking()
                        .FirstOrDefault(s => s.number_ic == u.ic_number);
                }

                if (app == null || app.cohortId <= 0)
                {
                    return null;
                }

                var cohortId = app.cohortId;
                var cohortLabel = cohorts.TryGetValue(cohortId, out var desc)
                    ? $"{cohortId} - {desc}"
                    : $"Cohort {cohortId}";
                return new StudentAccessItem
                {
                    StudentId = app.studentID,
                    StudentName = app.studentName,
                    Email = app.studentEmail,
                    AccessStatus = u.is_active ? "Active" : "Inactive",
                    CohortId = cohortId,
                    Cohort = cohortLabel
                };
            })
            .Where(x => x != null)
            .Select(x => x!)
            .ToList();

        StudentAccessGroups = StudentAccesses
            .GroupBy(s => new { s.CohortId, s.Cohort })
            .OrderBy(g => g.Key.CohortId == 0 ? int.MaxValue : g.Key.CohortId)
            .Select(g => new StudentAccessGroup
            {
                CohortId = g.Key.CohortId,
                CohortLabel = g.Key.Cohort,
                Students = g.OrderBy(x => x.StudentName).ToList()
            })
            .ToList();

    }


    public class StudentAccessItem
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string AccessStatus { get; set; } = string.Empty;
        public int CohortId { get; set; }
        public string Cohort { get; set; } = "-";
    }

    public class StudentAccessGroup
    {
        public int CohortId { get; set; }
        public string CohortLabel { get; set; } = "-";
        public List<StudentAccessItem> Students { get; set; } = new();
    }

}
