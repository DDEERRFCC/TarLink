using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ITPSystem.Pages.Supervisor
{
    public class ReportViewModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public ReportViewModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public ProgressReport? Report { get; private set; }
        public StudentApplication? Student { get; private set; }

        [BindProperty(SupportsGet = true)]
        public long reportId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? applicationId { get; set; }

        public IActionResult OnGet()
        {
            if (!IsSupervisor(out var supervisorStaffId, out var userEmail, out var userName))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            Report = _db.ProgressReports.AsNoTracking().FirstOrDefault(r => r.report_id == reportId);
            if (Report == null)
            {
                return RedirectToPage("/Supervisor/ReportResults", new { applicationId });
            }

            Student = _db.StudentApplications.AsNoTracking()
                .Include(s => s.Cohort)
                .FirstOrDefault(s => s.application_id == Report.applicantId);

            if (Student == null)
            {
                return RedirectToPage("/Supervisor/ReportResults", new { applicationId });
            }

            // Verify supervisor has access to this student
            if (!CanAccessStudent(supervisorStaffId, userEmail, userName, Student))
            {
                return RedirectToPage("/Supervisor/ReportResults", new { applicationId = Report.applicantId });
            }

            return Page();
        }

        public string GetReportTitle()
        {
            if (Report == null)
            {
                return "Report";
            }

            if (string.Equals(Report.reportType, "final", StringComparison.OrdinalIgnoreCase))
            {
                return "Final Report";
            }

            return Report.reportNo.HasValue ? $"Progress Report {Report.reportNo.Value}" : "Progress Report";
        }

        public string GetStatusLabel(byte? status)
        {
            return status switch
            {
                2 => "Approved",
                3 => "Rejected",
                1 => "Submitted",
                _ => "Pending"
            };
        }

        public string GetStatusBadgeClass(byte? status)
        {
            return status switch
            {
                2 => "bg-success",
                3 => "bg-danger",
                1 => "bg-warning text-dark",
                _ => "bg-secondary"
            };
        }

        private bool IsSupervisor(out string supervisorStaffId, out string userEmail, out string userName)
        {
            supervisorStaffId = string.Empty;
            userEmail = string.Empty;
            userName = string.Empty;
            var role = (HttpContext.Session.GetString("UserRole") ?? string.Empty).ToLowerInvariant();
            supervisorStaffId = HttpContext.Session.GetString("UserID") ?? string.Empty;
            userEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            userName = HttpContext.Session.GetString("UserName") ?? string.Empty;
            return role == "supervisor" && !string.IsNullOrWhiteSpace(supervisorStaffId) && !string.IsNullOrWhiteSpace(userEmail);
        }

        private static bool CanAccessStudent(string supervisorStaffId, string userEmail, string userName, StudentApplication student)
        {
            return string.Equals(student.ucSupervisorEmail, userEmail, StringComparison.OrdinalIgnoreCase)
                || string.Equals(student.comSupervisorEmail, userEmail, StringComparison.OrdinalIgnoreCase)
                || string.Equals(student.ucSupervisor, supervisorStaffId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(student.comSupervisor, supervisorStaffId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(student.ucSupervisor, userName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(student.comSupervisor, userName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
