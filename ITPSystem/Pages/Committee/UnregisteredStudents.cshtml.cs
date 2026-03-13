using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class CommitteeUnregisteredStudentsModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeUnregisteredStudentsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<UnregisteredStudentItem> UnregisteredStudents { get; private set; } = new();

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var cohorts = _db.Cohorts.AsNoTracking().ToDictionary(c => c.cohort_id, c => c.description ?? c.cohort_id.ToString());

        UnregisteredStudents = _db.SysUsers.AsNoTracking()
            .Where(u => u.role == "student" && !u.is_active)
            .OrderBy(u => u.username)
            .ToList()
            .Select(u =>
            {
                var app = u.application_id.HasValue
                    ? _db.StudentApplications.AsNoTracking().FirstOrDefault(s => s.application_id == u.application_id.Value)
                    : null;

                var cohortLabel = "-";
                if (app != null && cohorts.TryGetValue(app.cohortId, out var desc))
                {
                    cohortLabel = $"{app.cohortId} - {desc}";
                }

                return new UnregisteredStudentItem
                {
                    UserId = u.user_id,
                    StudentId = app?.studentID ?? u.username,
                    Name = app?.studentName ?? u.username,
                    Email = app?.studentEmail ?? u.email,
                    Cohort = cohortLabel
                };
            })
            .ToList();

        return Page();
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
