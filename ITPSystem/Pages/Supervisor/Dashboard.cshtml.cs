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
            Students = _db.StudentApplications.AsNoTracking()
                .Include(s => s.Cohort)
                .Where(s => s.ucSupervisorEmail == userEmail || s.comSupervisorEmail == userEmail)
                .OrderBy(s => s.studentName)
                .ToList();

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
                .GroupBy(s => new { s.cohortId, Label = string.IsNullOrWhiteSpace(s.Cohort?.description) ? $"Cohort {s.cohortId}" : s.Cohort.description })
                .OrderBy(g => g.Key.cohortId)
                .Select(g => new CohortGroup
                {
                    CohortId = g.Key.cohortId,
                    CohortLabel = g.Key.Label,
                    Students = g.OrderBy(x => x.studentName).ToList()
                })
                .ToList();

            return Page();
        }

        public class CohortGroup
        {
            public int CohortId { get; set; }
            public string CohortLabel { get; set; } = string.Empty;
            public List<StudentApplication> Students { get; set; } = new();
        }
    }
}

