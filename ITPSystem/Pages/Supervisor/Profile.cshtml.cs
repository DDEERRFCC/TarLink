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

        public UcSupervisor? Supervisor { get; set; }
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
                return RedirectToPage("/Login/SupervisorLogin");
            }

            Supervisor = _db.UcSupervisors.AsNoTracking().FirstOrDefault(u => u.email == userEmail);
            if (Supervisor == null)
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            DisplayName = Supervisor.name;

            AssignedStudentList = _db.StudentApplications.AsNoTracking()
                .Where(s => s.ucSupervisorEmail == userEmail || s.comSupervisorEmail == userEmail)
                .OrderBy(s => s.studentName)
                .ToList();

            AssignedStudents = AssignedStudentList.Count;
            PendingApplications = AssignedStudentList.Count(s => (s.applyStatus ?? string.Empty).ToLower() == "pending");
            ApprovedApplications = AssignedStudentList.Count(s => (s.applyStatus ?? string.Empty).ToLower() == "approved");
            RejectedApplications = AssignedStudentList.Count(s => (s.applyStatus ?? string.Empty).ToLower() == "rejected");

            var applicationIds = AssignedStudentList.Select(s => s.application_id).ToList();

            if (applicationIds.Count > 0)
            {
                var assessedApplicationIds = _db.AssessmentMarks.AsNoTracking()
                    .Where(m => applicationIds.Contains(m.application_id))
                    .Select(m => m.application_id)
                    .Distinct()
                    .ToHashSet();

                PendingReports = _db.ProgressReports.AsNoTracking()
                    .Count(r => applicationIds.Contains(r.applicantId)
                        && (
                            (r.reportType == "progress" && (r.status == 1 || r.status == 4))
                            || (r.reportType == "final"
                                && !string.IsNullOrWhiteSpace(r.file_path)
                                && (r.status == 1 || r.status == 2 || r.status == 4)
                                && !assessedApplicationIds.Contains(r.applicantId))
                        ));
                ApprovedReports = _db.ProgressReports.Count(r => applicationIds.Contains(r.applicantId) && r.status == 2);
                RejectedReports = _db.ProgressReports.Count(r => applicationIds.Contains(r.applicantId) && r.status == 3);

                DocumentReviewsPending = _db.DocumentReviews.Count(r => applicationIds.Contains(r.application_id) && r.status == "pending");
                DocumentReviewsApproved = _db.DocumentReviews.Count(r => applicationIds.Contains(r.application_id) && r.status == "approved");
                DocumentReviewsRejected = _db.DocumentReviews.Count(r => applicationIds.Contains(r.application_id) && r.status == "rejected");
            }

            // Note: Notifications flow FROM supervisors TO students, so supervisors don't receive notifications
            // These values remain 0 by default

            return Page();
        }

        private bool TryGetSupervisorContext(out string supervisorStaffId, out string userEmail)
        {
            supervisorStaffId = string.Empty;
            userEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            supervisorStaffId = HttpContext.Session.GetString("UserID") ?? string.Empty;
            var role = (HttpContext.Session.GetString("UserRole") ?? string.Empty).ToLowerInvariant();

            return role == "supervisor" && !string.IsNullOrWhiteSpace(userEmail) && !string.IsNullOrWhiteSpace(supervisorStaffId);
        }
    }
}
