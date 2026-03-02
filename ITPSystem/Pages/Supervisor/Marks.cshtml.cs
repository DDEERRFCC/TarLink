using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ITPSystem.Pages.Supervisor
{
    public class MarksModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public MarksModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public List<StudentApplication> Students { get; set; } = new();
        public List<MarkViewItem> MarkItems { get; set; } = new();

        [TempData]
        public string? Message { get; set; }

        public IActionResult OnGet()
        {
            if (!IsSupervisor(out _, out var userEmail))
            {
                return RedirectToPage("/Login/Login", new { role = "Supervisor" });
            }

            LoadData(userEmail);
            return Page();
        }

        public IActionResult OnPost(int applicationId, string rubricItem, decimal score, decimal maxScore, string? remarks)
        {
            if (!IsSupervisor(out var supervisorId, out var userEmail))
            {
                return RedirectToPage("/Login/Login", new { role = "Supervisor" });
            }

            _db.AssessmentMarks.Add(new AssessmentMark
            {
                application_id = applicationId,
                supervisor_user_id = supervisorId,
                rubric_item = rubricItem,
                score = score,
                max_score = maxScore,
                remarks = remarks,
                created_at = DateTime.UtcNow
            });
            _db.SaveChanges();

            var studentUser = _db.SysUsers.FirstOrDefault(u => u.application_id == applicationId);
            if (studentUser != null)
            {
                _db.Notifications.Add(new Notification
                {
                    from_user_id = supervisorId,
                    to_user_id = studentUser.user_id,
                    type = "assessment",
                    title = "New assessment mark published",
                    message = $"{rubricItem}: {score}/{maxScore}"
                });
                _db.SaveChanges();
            }

            Message = "Mark saved.";
            LoadData(userEmail);
            return Page();
        }

        private void LoadData(string userEmail)
        {
            Students = _db.StudentApplications.AsNoTracking()
                .Where(s => s.ucSupervisorEmail == userEmail || s.comSupervisorEmail == userEmail)
                .OrderBy(s => s.studentName)
                .ToList();

            var appMap = Students.ToDictionary(s => s.application_id, s => s.studentName);
            var appIds = appMap.Keys.ToList();

            MarkItems = _db.AssessmentMarks.AsNoTracking()
                .Where(m => appIds.Contains(m.application_id))
                .OrderByDescending(m => m.created_at)
                .ToList()
                .Select(m => new MarkViewItem
                {
                    Mark = m,
                    StudentName = appMap.TryGetValue(m.application_id, out var name) ? name : "Unknown"
                })
                .ToList();
        }

        private bool IsSupervisor(out int userId, out string userEmail)
        {
            userId = 0;
            userEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            var role = (HttpContext.Session.GetString("UserRole") ?? string.Empty).ToLowerInvariant();
            var rawUserId = HttpContext.Session.GetString("UserID");
            return role == "supervisor" && int.TryParse(rawUserId, out userId) && !string.IsNullOrWhiteSpace(userEmail);
        }

        public class MarkViewItem
        {
            public AssessmentMark Mark { get; set; } = new();
            public string StudentName { get; set; } = string.Empty;
        }
    }
}

