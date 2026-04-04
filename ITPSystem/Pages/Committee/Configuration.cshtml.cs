using ITPSystem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class CommitteeConfigurationModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeConfigurationModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<StudentAccessItem> StudentAccesses { get; private set; } = new();

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
        var students = _db.StudentApplications.AsNoTracking()
            .Include(s => s.Cohort)
            .ToDictionary(s => s.application_id);

        StudentAccesses = _db.SysUsers.AsNoTracking()
            .Where(u => u.role == "student")
            .OrderBy(u => u.username)
            .ToList()
            .Select(u =>
            {
                students.TryGetValue(u.application_id ?? -1, out var app);
                return new StudentAccessItem
                {
                    UserId = u.user_id,
                    StudentId = app?.studentID ?? u.username,
                    StudentName = app?.studentName ?? "-",
                    Email = string.IsNullOrWhiteSpace(u.email) ? "-" : u.email,
                    AccountStatus = u.is_active ? "Active" : "Inactive",
                    LockStatus = u.is_locked ? "Locked" : "Unlocked",
                    LastLogin = u.last_login_at?.ToString("yyyy-MM-dd HH:mm") ?? "-"
                };
            })
            .ToList();
    }

    public class StudentAccessItem
    {
        public int UserId { get; set; }
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string Email { get; set; } = "-";
        public string AccountStatus { get; set; } = string.Empty;
        public string LockStatus { get; set; } = string.Empty;
        public string LastLogin { get; set; } = "-";
    }
}
