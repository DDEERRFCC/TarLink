using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;

public class CommitteeUnregisteredStudentsModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeUnregisteredStudentsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<UnregisteredStudentItem> UnregisteredStudents { get; private set; } = new();
    public List<SelectListItem> CohortOptions { get; private set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var cohortList = _db.Cohorts.AsNoTracking()
            .Where(c => c.isActive)
            .OrderByDescending(c => c.startDate)
            .ToList();

        CohortOptions = cohortList
            .Select(c => new SelectListItem($"{c.cohort_id} - {c.description}", c.cohort_id.ToString()))
            .ToList();

        var cohorts = cohortList.ToDictionary(c => c.cohort_id, c => c.description ?? c.cohort_id.ToString());

        UnregisteredStudents = _db.SysUsers.AsNoTracking()
            .Where(u => u.role == "student" && !u.is_active)
            .OrderBy(u => u.username)
            .ToList()
            .Select(u =>
            {
                var app = u.application_id.HasValue
                    ? _db.StudentApplications.AsNoTracking().FirstOrDefault(s => s.application_id == u.application_id.Value)
                    : _db.StudentApplications.AsNoTracking().FirstOrDefault(s => s.studentEmail == u.email);

                var cohortLabel = "-";
                if (app != null && cohorts.TryGetValue(app.cohortId, out var desc))
                {
                    cohortLabel = $"{app.cohortId} - {desc}";
                }

                return new UnregisteredStudentItem
                {
                    UserId = u.user_id,
                    StudentId = app?.studentID ?? "-",
                    Name = app?.studentName ?? u.username,
                    Email = app?.studentEmail ?? u.email,
                    Cohort = cohortLabel
                };
            })
            .ToList();

        return Page();
    }

    public IActionResult OnPostAssignCohort(int userId, int cohortId)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var cohort = _db.Cohorts.AsNoTracking().FirstOrDefault(c => c.cohort_id == cohortId && c.isActive);
        if (cohort == null)
        {
            StatusMessage = "Selected cohort is not available.";
            return RedirectToPage();
        }

        var user = _db.SysUsers.FirstOrDefault(u => u.user_id == userId && u.role == "student");
        if (user == null)
        {
            StatusMessage = "Student account not found.";
            return RedirectToPage();
        }

        var app = user.application_id.HasValue
            ? _db.StudentApplications.FirstOrDefault(s => s.application_id == user.application_id.Value)
            : _db.StudentApplications.FirstOrDefault(s => s.studentEmail == user.email);

        if (app == null)
        {
            StatusMessage = "Student application not found.";
            return RedirectToPage();
        }

        user.application_id ??= app.application_id;
        app.cohortId = cohortId;
        app.updated_at = DateTime.Now;
        user.is_active = true;
        user.is_locked = false;

        _db.SaveChanges();

        StatusMessage = "Cohort assigned successfully.";
        return RedirectToPage();
    }

    public class UnregisteredStudentItem
    {
        public int UserId { get; set; }
        public string StudentId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Cohort { get; set; } = "-";
    }
}
