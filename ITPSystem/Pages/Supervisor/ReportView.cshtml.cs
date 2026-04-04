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
            if (!IsSupervisor(out _))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            Report = _db.ProgressReports.AsNoTracking().FirstOrDefault(r => r.report_id == reportId);
            if (Report == null)
            {
                return RedirectToPage("/Supervisor/ReportResults", new { applicationId });
            }

            Student = _db.StudentApplications.AsNoTracking()
                .FirstOrDefault(s => s.application_id == Report.applicantId);

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

        private bool IsSupervisor(out int userId)
        {
            userId = 0;
            var role = (HttpContext.Session.GetString("UserRole") ?? string.Empty).ToLowerInvariant();
            var rawUserId = HttpContext.Session.GetString("UserID");
            return role == "supervisor" && int.TryParse(rawUserId, out userId);
        }
    }
}
