using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ITPSystem.Pages.Supervisor
{
    public class StudentsModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public StudentsModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public List<StudentApplication> Students { get; set; } = new();
        private Dictionary<int, Cohort> CohortMap { get; set; } = new();

        [TempData]
        public string? Message { get; set; }

        public IActionResult OnGet()
        {
            if (!IsSupervisor(out _, out _))
            {
                return RedirectToPage("/Login/Login", new { role = "Supervisor" });
            }

            LoadStudents();
            return Page();
        }

        public IActionResult OnPostApprove(int applicationId, string? remarks)
        {
            return UpdateApplicationStatus(applicationId, "approved", "approved", remarks);
        }

        public IActionResult OnPostReject(int applicationId, string? remarks)
        {
            return UpdateApplicationStatus(applicationId, "rejected", "rejected", remarks);
        }

        public string GetCohortRange(int cohortId)
        {
            if (!CohortMap.TryGetValue(cohortId, out var cohort))
            {
                return "-";
            }

            var start = cohort.startDate?.ToString("yyyy-MM-dd") ?? "-";
            var end = cohort.endDate?.ToString("yyyy-MM-dd") ?? "-";
            return $"{start} to {end}";
        }

        private IActionResult UpdateApplicationStatus(int applicationId, string newStatus, string actionLabel, string? remarks)
        {
            if (!IsSupervisor(out var supervisorId, out var userEmail))
            {
                return RedirectToPage("/Login/Login", new { role = "Supervisor" });
            }

            var studentApp = _db.StudentApplications.FirstOrDefault(s =>
                s.application_id == applicationId &&
                (s.ucSupervisorEmail == userEmail || s.comSupervisorEmail == userEmail));

            if (studentApp == null)
            {
                Message = "Student application not found or not assigned to you.";
                return RedirectToPage();
            }

            if (!string.Equals(studentApp.applyStatus, "pending", StringComparison.OrdinalIgnoreCase))
            {
                Message = "This student application has already been processed.";
                return RedirectToPage();
            }

            studentApp.applyStatus = newStatus;
            if (!string.IsNullOrWhiteSpace(remarks))
            {
                studentApp.remark = remarks.Trim();
            }

            _db.SaveChanges();

            var studentUser = _db.SysUsers.FirstOrDefault(u => u.application_id == applicationId);
            if (studentUser != null)
            {
                _db.Notifications.Add(new Notification
                {
                    from_user_id = supervisorId,
                    to_user_id = studentUser.user_id,
                    type = "student_application",
                    title = $"Your internship application was {actionLabel}",
                    message = string.IsNullOrWhiteSpace(remarks)
                        ? "Please check your internship application status."
                        : remarks.Trim()
                });
                _db.SaveChanges();
            }

            Message = $"Student application {actionLabel} successfully.";
            return RedirectToPage();
        }

        private void LoadStudents()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            CohortMap = _db.Cohorts.AsNoTracking().ToDictionary(c => c.cohort_id);
            Students = _db.StudentApplications.AsNoTracking()
                .Where(s => s.ucSupervisorEmail == userEmail || s.comSupervisorEmail == userEmail)
                .OrderBy(s => s.applyStatus == "pending" ? 0 : 1)
                .ThenBy(s => s.studentName)
                .ToList();
        }

        private bool IsSupervisor(out int userId, out string userEmail)
        {
            userId = 0;
            userEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            var role = (HttpContext.Session.GetString("UserRole") ?? string.Empty).ToLowerInvariant();
            var rawUserId = HttpContext.Session.GetString("UserID");
            return role == "supervisor"
                && int.TryParse(rawUserId, out userId)
                && !string.IsNullOrWhiteSpace(userEmail);
        }
    }
}

