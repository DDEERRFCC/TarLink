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

    public IActionResult OnPostRemoveAccess(int userId)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var user = _db.SysUsers.FirstOrDefault(u => u.user_id == userId && u.role == "student");
        if (user == null)
        {
            StatusMessage = "Student user not found.";
            return RedirectToPage();
        }

        if (!user.is_active)
        {
            StatusMessage = "Student access is already inactive.";
            return RedirectToPage();
        }

        user.is_active = false;
        _db.SaveChanges();

        StatusMessage = "Student access removed successfully.";
        return RedirectToPage();
    }

    private void LoadData()
    {
        var cohorts = _db.Cohorts.AsNoTracking()
            .ToDictionary(c => c.cohort_id, c => new
            {
                Label = c.description ?? $"Cohort {c.cohort_id}",
                c.isActive
            });

        var students = _db.StudentApplications.AsNoTracking()
            .Include(s => s.Cohort)
            .ToDictionary(s => s.application_id);
        var sysUsers = _db.SysUsers
            .Where(u => u.role == "student")
            .OrderBy(u => u.username)
            .ToList();

        StudentAccesses = sysUsers
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
                var cohortLabel = cohorts.TryGetValue(cohortId, out var meta)
                    ? $"{cohortId} - {meta.Label}"
                    : $"Cohort {cohortId}";
                var cohortActive = cohorts.TryGetValue(cohortId, out var cohortMeta) && cohortMeta.isActive;
                return new StudentAccessItem
                {
                    UserId = u.user_id,
                    StudentId = app.studentID,
                    StudentName = app.studentName,
                    Email = app.studentEmail,
                    AccessStatus = cohortActive ? (u.is_locked ? "Locked" : (u.is_active ? "Active" : "Inactive")) : "Locked",
                    CohortId = cohortId,
                    Cohort = cohortLabel
                };
            })
            .Where(x => x != null)
            .Select(x => x!)
            .ToList();

        var expiredUserIds = StudentAccesses
            .Where(s => string.Equals(s.AccessStatus, "Locked", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.UserId)
            .ToList();

        if (expiredUserIds.Count > 0)
        {
            var lockTargets = sysUsers
                .Where(u => expiredUserIds.Contains(u.user_id))
                .ToList();

            var updated = false;
            foreach (var user in lockTargets)
            {
                if (!user.is_locked)
                {
                    user.is_locked = true;
                    updated = true;
                }
            }

            if (updated)
            {
                _db.SaveChanges();
            }
        }

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
        public int UserId { get; set; }
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
