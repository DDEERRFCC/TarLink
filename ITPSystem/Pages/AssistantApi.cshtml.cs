using ITPSystem.Data;
using ITPSystem.Models;
using ITPSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

public class AssistantApiModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly OllamaStudentAssistantService _assistantService;

    public AssistantApiModel(ApplicationDbContext db, OllamaStudentAssistantService assistantService)
    {
        _db = db;
        _assistantService = assistantService;
    }

    public IActionResult OnGet()
    {
        return NotFound();
    }

    public async Task<IActionResult> OnPostAskAsync([FromForm] string question, [FromForm] string? currentPage, CancellationToken cancellationToken)
    {
        var trimmedQuestion = (question ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmedQuestion))
        {
            return new JsonResult(new { success = false, answer = "Please type a question first." });
        }

        var context = BuildAssistantContext(currentPage);
        var answer = await _assistantService.AskAsync(context, trimmedQuestion, cancellationToken);
        return new JsonResult(new { success = true, answer });
    }

    private StudentAssistantContext BuildAssistantContext(string? currentPage)
    {
        var role = (HttpContext.Session.GetString("UserRole") ?? "guest").Trim().ToLowerInvariant();
        var context = new StudentAssistantContext
        {
            UserRole = string.IsNullOrWhiteSpace(role) ? "guest" : role,
            StudentName = HttpContext.Session.GetString("UserName") ?? "Guest",
            StudentId = HttpContext.Session.GetString("StudentID") ?? "-",
            CurrentPage = string.IsNullOrWhiteSpace(currentPage) ? "-" : currentPage.Trim(),
            Status = "-",
            Cohort = "-",
            InternPeriod = "-"
        };

        if (role != "student")
        {
            return context;
        }

        var userIdText = HttpContext.Session.GetString("UserID");
        if (!int.TryParse(userIdText, out var userId))
        {
            return context;
        }

        var user = _db.SysUsers.AsNoTracking().FirstOrDefault(u => u.user_id == userId);
        if (user?.application_id == null)
        {
            return context;
        }

        var student = _db.StudentApplications.AsNoTracking()
            .Include(s => s.Cohort)
            .FirstOrDefault(s => s.application_id == user.application_id.Value);

        if (student == null)
        {
            return context;
        }

        context.StudentName = string.IsNullOrWhiteSpace(student.studentName) ? context.StudentName : student.studentName;
        context.StudentId = string.IsNullOrWhiteSpace(student.studentID) ? context.StudentId : student.studentID;
        context.Status = string.IsNullOrWhiteSpace(student.applyStatus) ? "-" : student.applyStatus;
        context.Cohort = string.IsNullOrWhiteSpace(student.Cohort?.description) ? student.cohortId.ToString() : student.Cohort.description!;
        context.InternPeriod = student.Cohort?.startDate != null && student.Cohort.endDate != null
            ? $"{student.Cohort.startDate:yyyy-MM-dd} to {student.Cohort.endDate:yyyy-MM-dd}"
            : "-";

        var submittedReportKeys = _db.ProgressReports.AsNoTracking()
            .Where(r => r.applicantId == student.application_id)
            .Select(r => new { r.reportType, r.reportNo })
            .ToList()
            .Select(r => string.Equals(r.reportType, "final", StringComparison.OrdinalIgnoreCase) ? "F" : $"P{r.reportNo}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        void AddDeadline(string key, string title, DateTime? dueDate)
        {
            if (!dueDate.HasValue || submittedReportKeys.Contains(key))
            {
                return;
            }

            context.Deadlines.Add(new StudentAssistantDeadlineItem
            {
                Title = title,
                DateText = dueDate.Value.ToString("yyyy-MM-dd"),
                Note = dueDate.Value.Date < DateTime.Today ? "Overdue" : "Upcoming"
            });
        }

        AddDeadline("P1", "Progress Report 1", student.Cohort?.report1DueDate);
        AddDeadline("P2", "Progress Report 2", student.Cohort?.report2DueDate);
        AddDeadline("P3", "Progress Report 3", student.Cohort?.report3DueDate);
        AddDeadline("P4", "Progress Report 4", student.Cohort?.report4DueDate);
        AddDeadline("P5", "Progress Report 5", student.Cohort?.report5DueDate);
        AddDeadline("P6", "Progress Report 6", student.Cohort?.report6DueDate);
        AddDeadline("F", "Final Report", student.Cohort?.finalReportDueDate);

        return context;
    }
}
