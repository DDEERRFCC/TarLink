using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ITPSystem.Pages.Supervisor
{
    public class ProfileModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public ProfileModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public SysUser? Supervisor { get; set; }
        public string DisplayName { get; set; } = string.Empty;

        public int AssignedStudents { get; set; }
        public int PendingApplications { get; set; }
        public int ApprovedApplications { get; set; }
        public int RejectedApplications { get; set; }

        public int PendingReports { get; set; }
        public int ApprovedReports { get; set; }
        public int RejectedReports { get; set; }

        public int UnreadNotifications { get; set; }
        public int TotalNotifications { get; set; }

        public int DocumentReviewsApproved { get; set; }
        public int DocumentReviewsRejected { get; set; }
        public int DocumentReviewsPending { get; set; }

        public List<StudentApplication> AssignedStudentList { get; set; } = new();
        public List<Notification> RecentNotifications { get; set; } = new();

        public IActionResult OnGet()
        {
            if (!TryGetSupervisorContext(out var userId, out var userEmail))
            {
                return RedirectToPage("/Login/Login", new { role = "Supervisor" });
            }

            Supervisor = _db.SysUsers.AsNoTracking().FirstOrDefault(u => u.user_id == userId);
            if (Supervisor == null)
            {
                return RedirectToPage("/Login/Login", new { role = "Supervisor" });
            }

            DisplayName = string.IsNullOrWhiteSpace(Supervisor.username) ? Supervisor.email : Supervisor.username;

            AssignedStudentList = _db.StudentApplications.AsNoTracking()
                .Where(s => s.ucSupervisorEmail == userEmail || s.comSupervisorEmail == userEmail)
                .OrderBy(s => s.studentName)
                .ToList();

            AssignedStudents = AssignedStudentList.Count;
            PendingApplications = AssignedStudentList.Count(s => (s.applyStatus ?? string.Empty).ToLower() == "pending");
            ApprovedApplications = AssignedStudentList.Count(s => (s.applyStatus ?? string.Empty).ToLower() == "approved");
            RejectedApplications = AssignedStudentList.Count(s => (s.applyStatus ?? string.Empty).ToLower() == "rejected");

            var applicationIds = AssignedStudentList.Select(s => (long)s.application_id).ToList();

            if (applicationIds.Count > 0)
            {
                PendingReports = _db.ProgressReports.Count(r => r.applicantId.HasValue && applicationIds.Contains(r.applicantId.Value) && (r.status ?? 0) == 0);
                ApprovedReports = _db.ProgressReports.Count(r => r.applicantId.HasValue && applicationIds.Contains(r.applicantId.Value) && (r.status ?? 0) == 1);
                RejectedReports = _db.ProgressReports.Count(r => r.applicantId.HasValue && applicationIds.Contains(r.applicantId.Value) && (r.status ?? 0) == 2);

                DocumentReviewsPending = _db.DocumentReviews.Count(r => applicationIds.Contains(r.application_id) && r.status == "pending");
                DocumentReviewsApproved = _db.DocumentReviews.Count(r => applicationIds.Contains(r.application_id) && r.status == "approved");
                DocumentReviewsRejected = _db.DocumentReviews.Count(r => applicationIds.Contains(r.application_id) && r.status == "rejected");
            }

            UnreadNotifications = _db.Notifications.Count(n => n.to_user_id == userId && !n.is_read);
            TotalNotifications = _db.Notifications.Count(n => n.to_user_id == userId);
            RecentNotifications = _db.Notifications.AsNoTracking()
                .Where(n => n.to_user_id == userId)
                .OrderByDescending(n => n.created_at)
                .Take(5)
                .ToList();

            return Page();
        }

        private bool TryGetSupervisorContext(out int userId, out string userEmail)
        {
            userId = 0;
            userEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            var rawUserId = HttpContext.Session.GetString("UserID");
            var role = (HttpContext.Session.GetString("UserRole") ?? string.Empty).ToLowerInvariant();

            return role == "supervisor"
                && int.TryParse(rawUserId, out userId)
                && !string.IsNullOrWhiteSpace(userEmail);
        }
    }
}
