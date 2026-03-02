using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ITPSystem.Pages.Supervisor
{
    public class ReportsModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public ReportsModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public List<ReportViewItem> Items { get; set; } = new();

        [TempData]
        public string? Message { get; set; }

        public IActionResult OnGet()
        {
            if (!IsSupervisor(out _))
            {
                return RedirectToPage("/Login/Login", new { role = "Supervisor" });
            }

            LoadItems();
            return Page();
        }

        public IActionResult OnPostApprove(long reportId, string? remarks)
        {
            return UpdateReport(reportId, 1, remarks, "approved");
        }

        public IActionResult OnPostReject(long reportId, string? remarks)
        {
            return UpdateReport(reportId, 2, remarks, "rejected");
        }

        public string GetStatusLabel(byte? status)
        {
            return status switch
            {
                1 => "Approved",
                2 => "Rejected",
                _ => "Pending"
            };
        }

        private IActionResult UpdateReport(long reportId, byte status, string? remarks, string action)
        {
            if (!IsSupervisor(out var supervisorId))
            {
                return RedirectToPage("/Login/Login", new { role = "Supervisor" });
            }

            var report = _db.ProgressReports.FirstOrDefault(r => r.report_id == reportId);
            if (report == null)
            {
                Message = "Report not found.";
                return RedirectToPage();
            }

            report.status = status;
            report.remark = remarks;
            report.lastUpdate = DateTime.UtcNow;
            _db.SaveChanges();

            if (report.applicantId.HasValue)
            {
                var studentUser = _db.SysUsers.FirstOrDefault(u => u.application_id == (int)report.applicantId.Value);
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
            return RedirectToPage();
        }

        private void LoadItems()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            var students = _db.StudentApplications.AsNoTracking()
                .Where(s => s.ucSupervisorEmail == userEmail || s.comSupervisorEmail == userEmail)
                .ToDictionary(s => s.application_id);

            var studentAppIds = students.Keys.Select(id => (long)id).ToList();

            Items = _db.ProgressReports.AsNoTracking()
                .Where(r => r.applicantId.HasValue && studentAppIds.Contains(r.applicantId.Value))
                .OrderBy(r => r.dueDate)
                .ToList()
                .Select(r =>
                {
                    students.TryGetValue((int)(r.applicantId ?? -1), out var student);
                    return new ReportViewItem
                    {
                        Report = r,
                        StudentName = student?.studentName ?? "Unknown",
                        StudentId = student?.studentID ?? "-"
                    };
                })
                .ToList();
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
    }
}

