using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ITPSystem.Pages.Supervisor
{
    public class ReportResultsModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public ReportResultsModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public List<ReportViewItem> Items { get; private set; } = new();
        public List<StudentReportSummary> StudentSummaries { get; private set; } = new();
        public string? SelectedStudent { get; private set; }

        [BindProperty(SupportsGet = true)]
        public string reportFilter { get; set; } = "all";

        [BindProperty(SupportsGet = true)]
        public int? applicationId { get; set; }

        [TempData]
        public string? Message { get; set; }

        public IActionResult OnGet()
        {
            if (!IsSupervisor(out _))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            LoadItems();
            return Page();
        }

        public IActionResult OnPostApprove(long reportId, string? remarks, int? applicationId)
        {
            return UpdateReport(reportId, 2, remarks, "approved", applicationId);
        }

        public IActionResult OnPostReject(long reportId, string? remarks, int? applicationId)
        {
            return UpdateReport(reportId, 3, remarks, "rejected", applicationId);
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

        public string GetReportTitle(ProgressReport report)
        {
            if (string.Equals(report.reportType, "final", StringComparison.OrdinalIgnoreCase))
            {
                return "Final Report";
            }

            return report.reportNo.HasValue ? $"Progress Report {report.reportNo.Value}" : "Progress Report";
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

        private IActionResult UpdateReport(long reportId, byte status, string? remarks, string action, int? selectedApplicationId)
        {
            if (!IsSupervisor(out var supervisorId))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            var report = _db.ProgressReports.FirstOrDefault(r => r.report_id == reportId);
            if (report == null)
            {
                Message = "Report not found.";
                return RedirectToPage(new { reportFilter, applicationId = selectedApplicationId });
            }

            report.status = status;
            report.remark = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
            report.updated_at = DateTime.UtcNow;
            _db.SaveChanges();

            if (report.applicantId > 0)
            {
                var studentUser = _db.SysUsers.FirstOrDefault(u => u.application_id == report.applicantId);
                if (studentUser != null)
                {
                    _db.Notifications.Add(new Notification
                    {
                        from_user_id = supervisorId,
                        to_user_id = studentUser.user_id,
                        type = "progress_report",
                        title = $"Your progress report was {action}",
                        message = string.IsNullOrWhiteSpace(remarks) ? "Please check your progress report status." : remarks
                    });
                    _db.SaveChanges();
                }
            }

            Message = $"Report {action} successfully.";
            return RedirectToPage(new { reportFilter, applicationId = report.applicantId });
        }

        private void LoadItems()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            var userName = HttpContext.Session.GetString("UserName") ?? string.Empty;

            var students = _db.StudentApplications.AsNoTracking()
                .Where(s =>
                    s.ucSupervisorEmail == userEmail ||
                    s.comSupervisorEmail == userEmail ||
                    s.ucSupervisor == userName ||
                    s.comSupervisor == userName)
                .ToDictionary(s => s.application_id);

            var studentAppIds = students.Keys.ToList();
            if (applicationId.HasValue)
            {
                studentAppIds = studentAppIds.Where(id => id == applicationId.Value).ToList();
            }

            Items = _db.ProgressReports.AsNoTracking()
                .Where(r => studentAppIds.Contains(r.applicantId))
                .OrderByDescending(r => r.updated_at)
                .ThenBy(r => r.dueDate)
                .ToList()
                .Where(r => MatchesFilter(r, reportFilter))
                .Select(r =>
                {
                    students.TryGetValue(r.applicantId, out var student);
                    return new ReportViewItem
                    {
                        Report = r,
                        StudentName = student?.studentName ?? "Unknown",
                        StudentId = student?.studentID ?? "-"
                    };
                })
                .ToList();

            if (applicationId.HasValue && students.TryGetValue(applicationId.Value, out var selected))
            {
                SelectedStudent = $"{selected.studentName} ({selected.studentID})";
            }

            StudentSummaries = Items
                .GroupBy(x => new { x.Report.applicantId, x.StudentName, x.StudentId })
                .Select(g =>
                {
                    var ordered = g
                        .OrderBy(x => x.Report.reportType == "final" ? 1 : 0)
                        .ThenBy(x => x.Report.reportNo ?? 99)
                        .ToList();

                    var latest = ordered.OrderByDescending(x => x.Report.updated_at).First();
                    return new StudentReportSummary
                    {
                        ApplicantId = g.Key.applicantId,
                        StudentName = g.Key.StudentName,
                        StudentId = g.Key.StudentId,
                        ReportItems = ordered,
                        LatestUpdatedAt = latest.Report.updated_at,
                        LatestRemark = latest.Report.remark
                    };
                })
                .OrderBy(x => x.StudentName)
                .ToList();
        }

        private static bool MatchesFilter(ProgressReport report, string? filter)
        {
            var key = (filter ?? "all").Trim().ToLowerInvariant();
            if (key == "all")
            {
                return true;
            }

            if (key == "f")
            {
                return string.Equals(report.reportType, "final", StringComparison.OrdinalIgnoreCase);
            }

            if (key.StartsWith("p") && byte.TryParse(key[1..], out var reportNo))
            {
                return string.Equals(report.reportType, "progress", StringComparison.OrdinalIgnoreCase)
                    && report.reportNo == reportNo;
            }

            return true;
        }

        private bool IsSupervisor(out int userId)
        {
            userId = 0;
            var role = (HttpContext.Session.GetString("UserRole") ?? string.Empty).ToLowerInvariant();
            var rawUserId = HttpContext.Session.GetString("UserID");
            return role == "supervisor" && int.TryParse(rawUserId, out userId);
        }

        public class ReportViewItem
        {
            public ProgressReport Report { get; set; } = new();
            public string StudentName { get; set; } = string.Empty;
            public string StudentId { get; set; } = string.Empty;
        }

        public class StudentReportSummary
        {
            public int ApplicantId { get; set; }
            public string StudentName { get; set; } = string.Empty;
            public string StudentId { get; set; } = string.Empty;
            public List<ReportViewItem> ReportItems { get; set; } = new();
            public DateTime LatestUpdatedAt { get; set; }
            public string? LatestRemark { get; set; }
        }
    }
}
