using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ITPSystem.Pages.Supervisor
{
    public class DashboardModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public DashboardModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public string UserName { get; set; } = string.Empty;
        public int PendingReports { get; set; }
        public int PendingDocuments { get; set; }
        public int PendingApplications { get; set; }
        public int ActiveInternships { get; set; }
        public int UnreadNotifications { get; set; }
        public List<StudentApplication> Students { get; set; } = new();
        public List<CohortGroup> CohortGroups { get; private set; } = new();

        public IActionResult OnGet()
        {
            var userRole = (HttpContext.Session.GetString("UserRole") ?? string.Empty).ToLowerInvariant();
            var userEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            var userIdRaw = HttpContext.Session.GetString("UserID");

            if (string.IsNullOrWhiteSpace(userEmail) || userRole != "supervisor")
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            UserName = HttpContext.Session.GetString("UserName") ?? userEmail;
            var assignedApps = _db.StudentApplications.AsNoTracking()
                .Include(s => s.Cohort)
                .Where(s => s.ucSupervisorEmail == userEmail || s.comSupervisorEmail == userEmail)
                .OrderBy(s => s.studentName)
                .ToList();

            var sysUsers = _db.SysUsers
                .Where(u => u.role == "student")
                .ToList();

            var userByAppId = sysUsers
                .Where(u => u.application_id.HasValue)
                .ToDictionary(u => u.application_id!.Value, u => u);

            var userByEmail = sysUsers
                .Where(u => !string.IsNullOrWhiteSpace(u.email))
                .ToDictionary(u => u.email!.Trim(), u => u, StringComparer.OrdinalIgnoreCase);

            var userByIc = sysUsers
                .Where(u => !string.IsNullOrWhiteSpace(u.ic_number))
                .ToDictionary(u => u.ic_number!.Trim(), u => u, StringComparer.OrdinalIgnoreCase);

            var lockedUserIds = new HashSet<int>();
            var filtered = new List<StudentApplication>();

            foreach (var app in assignedApps)
            {
                SysUser? user = null;
                if (userByAppId.TryGetValue(app.application_id, out var byApp))
                {
                    user = byApp;
                }
                else if (!string.IsNullOrWhiteSpace(app.studentEmail) && userByEmail.TryGetValue(app.studentEmail, out var byEmail))
                {
                    user = byEmail;
                }
                else if (!string.IsNullOrWhiteSpace(app.number_ic) && userByIc.TryGetValue(app.number_ic, out var byIc))
                {
                    user = byIc;
                }

                if (user == null || !user.is_active)
                {
                    continue;
                }

                if (app.Cohort != null && !app.Cohort.isActive && !user.is_locked)
                {
                    user.is_locked = true;
                    lockedUserIds.Add(user.user_id);
                }

                filtered.Add(app);
            }

            if (lockedUserIds.Count > 0)
            {
                _db.SaveChanges();
            }

            Students = filtered;

            var applicationIds = Students.Select(s => s.application_id).ToList();

            PendingReports = _db.ProgressReports.Count(r => applicationIds.Contains(r.applicantId) && r.status == 1);
            PendingDocuments = _db.DocumentReviews.Count(r => r.status == "pending");
            PendingApplications = Students.Count(s => s.applyStatus == "pending");
            ActiveInternships = Students.Count(s => s.applyStatus == "approved");

            if (int.TryParse(userIdRaw, out var userId))
            {
                UnreadNotifications = _db.Notifications.Count(n => n.to_user_id == userId && !n.is_read);
            }

            CohortGroups = Students
                .GroupBy(s => s.cohortId)
                .Select(g =>
                {
                    var cohort = g.FirstOrDefault()?.Cohort;
                    var baseLabel = string.IsNullOrWhiteSpace(cohort?.description)
                        ? $"Cohort {g.Key}"
                        : $"Cohort {g.Key} - {cohort.description}";
                    var isActive = cohort?.isActive ?? false;
                    var label = isActive ? baseLabel : $"History Student : {baseLabel} (Expired)";
                    return new CohortGroup
                    {
                        CohortId = g.Key,
                        CohortLabel = label,
                        IsHistory = !isActive,
                        SortDate = cohort?.startDate,
                        Students = g.OrderBy(x => x.studentName).ToList()
                    };
                })
                .OrderByDescending(g => !g.IsHistory)
                .ThenByDescending(g => g.SortDate ?? DateTime.MinValue)
                .ToList();

            return Page();
        }

        public class CohortGroup
        {
            public int CohortId { get; set; }
            public string CohortLabel { get; set; } = string.Empty;
            public List<StudentApplication> Students { get; set; } = new();
            public bool IsHistory { get; set; }
            public DateTime? SortDate { get; set; }
        }
    }
}

