using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
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
        public List<StudentApplication> CurrentStudents { get; set; } = new();
        public List<StudentApplication> HistoryStudents { get; set; } = new();
        private Dictionary<int, Cohort> CohortMap { get; set; } = new();
        public List<SelectListItem> CohortOptions { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public byte? level { get; set; }


        [TempData]
        public string? Message { get; set; }

        public IActionResult OnGet()
        {
            if (!IsSupervisor(out _, out _))
            {
                return RedirectToPage("/Login/SupervisorLogin");
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

        public IActionResult OnPostUpdateCohort(int applicationId, int cohortId)
        {
            if (!IsSupervisor(out _, out var userEmail))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            var studentApp = _db.StudentApplications.FirstOrDefault(s =>
                s.application_id == applicationId &&
                (s.ucSupervisorEmail == userEmail || s.comSupervisorEmail == userEmail));

            if (studentApp == null)
            {
                Message = "Student application not found or not assigned to you.";
                return RedirectToPage();
            }

            var cohortExists = _db.Cohorts.Any(c => c.cohort_id == cohortId);
            if (!cohortExists)
            {
                Message = "Selected cohort was not found.";
                return RedirectToPage();
            }

            if (studentApp.cohortId == cohortId)
            {
                Message = "Student is already assigned to the selected cohort.";
                return RedirectToPage();
            }

            studentApp.cohortId = cohortId;
            _db.SaveChanges();

            Message = "Student cohort updated successfully.";
            return RedirectToPage(new { level = this.level });
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

        public DateTime? GetCohortStartDate(int cohortId)
        {
            if (!CohortMap.TryGetValue(cohortId, out var cohort))
            {
                return null;
            }

            return cohort.startDate;
        }

        public string GetCohortHeader(int cohortId)
        {
            if (!CohortMap.TryGetValue(cohortId, out var cohort))
            {
                return $"Cohort {cohortId}";
            }

            var range = GetCohortRangeLabel(cohort);
            var rangeHasLevel = range.Contains("level", StringComparison.OrdinalIgnoreCase);
            var levelPart = (!rangeHasLevel && cohort.level.HasValue) ? $" (Level {cohort.level.Value})" : string.Empty;
            return $"Cohort {range}{levelPart}";
        }

        private static string GetCohortRangeLabel(Cohort cohort)
        {
            if (!string.IsNullOrWhiteSpace(cohort.description))
            {
                return cohort.description;
            }

            var start = cohort.startDate?.ToString("MMM yyyy") ?? "-";
            var end = cohort.endDate?.ToString("MMM yyyy") ?? "-";
            return $"{start} - {end}";
        }

        public static string GetLevelLabel(byte? value)
        {
            return value switch
            {
                0 => "0 - Unknown",
                1 => "1 - Diploma",
                2 => "2 - Degree",
                3 => "3 - Master",
                4 => "4 - PhD",
                _ => "All"
            };
        }

        private IActionResult UpdateApplicationStatus(int applicationId, string newStatus, string actionLabel, string? remarks)
        {
            if (!IsSupervisor(out var supervisorId, out var userEmail))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            var studentApp = _db.StudentApplications.FirstOrDefault(s =>
                s.application_id == applicationId &&
                (s.ucSupervisorEmail == userEmail || s.comSupervisorEmail == userEmail));

            if (studentApp == null)
            {
                Message = "Student application not found or not assigned to you.";
                return RedirectToPage();
            }

            var currentStatus = (studentApp.applyStatus ?? string.Empty).Trim().ToLowerInvariant();

            if (string.Equals(newStatus, "approved", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(currentStatus, "pending", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(currentStatus, "rejected", StringComparison.OrdinalIgnoreCase))
            {
                Message = "Only pending or rejected applications can be approved.";
                return RedirectToPage();
            }

            if (string.Equals(newStatus, "rejected", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(currentStatus, "rejected", StringComparison.OrdinalIgnoreCase))
                {
                    Message = "This student application is already rejected.";
                    return RedirectToPage();
                }

                if (string.IsNullOrWhiteSpace(remarks))
                {
                    Message = "Please provide rejection remarks/reason.";
                    return RedirectToPage();
                }
            }

            if (!string.Equals(newStatus, "approved", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(newStatus, "rejected", StringComparison.OrdinalIgnoreCase))
            {
                Message = "Unsupported status update.";
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
            var cohorts = _db.Cohorts.AsNoTracking()
                .OrderByDescending(c => c.startDate)
                .ToList();

            CohortMap = cohorts.ToDictionary(c => c.cohort_id);
            CohortOptions = cohorts
                .Select(c =>
                {
                    var label = GetCohortRangeLabel(c);
                    if (c.level.HasValue)
                    {
                        label = $"{label} (Level {c.level.Value})";
                    }

                    return new SelectListItem(label, c.cohort_id.ToString());
                })
                .ToList();

            var query = _db.StudentApplications.AsNoTracking()
                .Where(s => s.ucSupervisorEmail == userEmail || s.comSupervisorEmail == userEmail)
                .AsQueryable();

            if (level.HasValue)
            {
                query = query.Where(s => s.level == level.Value);
            }

            Students = query
                .OrderBy(s => s.applyStatus == "pending" ? 0 : 1)
                .ThenBy(s => s.studentName)
                .ToList();

            CurrentStudents = Students
                .Where(s => !CohortMap.TryGetValue(s.cohortId, out var cohort) || cohort.isActive)
                .ToList();

            HistoryStudents = Students
                .Where(s => CohortMap.TryGetValue(s.cohortId, out var cohort) && !cohort.isActive)
                .ToList();

            // CurrentStudents and HistoryStudents are available for separate display.
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

