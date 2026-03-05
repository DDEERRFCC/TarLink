using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

public class StudentMyProgressReportModel : PageModel
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;

    public StudentMyProgressReportModel(ApplicationDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public List<ProgressReport> Reports { get; private set; } = new();
    public List<ReportOptionItem> ReportTypes { get; private set; } = new();

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Please select a report.")]
        public string ReportKey { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your report content.")]
        [StringLength(4000, ErrorMessage = "Report content is too long (max 4000 characters).")]
        public string Content { get; set; } = string.Empty;

        public IFormFile? ReportFile { get; set; }
    }

    public class ReportOptionItem
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
    }

    public IActionResult OnGet()
    {
        if (!TryGetApprovedStudent(out var student))
        {
            return RedirectToPage("/Student/Dashboard");
        }

        BuildReportTypes(student.Cohort);
        LoadReports(student.application_id);
        return Page();
    }

    public IActionResult OnPostSubmit()
    {
        if (!TryGetApprovedStudent(out var student))
        {
            return RedirectToPage("/Student/Dashboard");
        }

        BuildReportTypes(student.Cohort);
        if (!TryParseReportKey(Input.ReportKey, out var reportType, out var reportNo))
        {
            ModelState.AddModelError(nameof(Input.ReportKey), "Invalid report type.");
        }
        ValidateReportFile(Input.ReportFile);

        if (!ModelState.IsValid)
        {
            LoadReports(student.application_id);
            return Page();
        }

        var content = Input.Content.Trim();
        var savedFilePath = SaveReportFile(Input.ReportFile);

        var dueDate = GetDueDate(reportType, reportNo, student.Cohort) ?? DateTime.UtcNow.Date;
        var existing = _db.ProgressReports
            .FirstOrDefault(r =>
                r.applicantId == student.application_id &&
                r.cohortId == student.cohortId &&
                r.reportType == reportType &&
                r.reportNo == reportNo);

        if (existing == null)
        {
            _db.ProgressReports.Add(new ProgressReport
            {
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow,
                applicantId = student.application_id,
                cohortId = student.cohortId,
                reportType = reportType,
                reportNo = reportNo,
                dueDate = dueDate,
                status = 1,
                remark = content,
                file_path = savedFilePath
            });
        }
        else
        {
            existing.updated_at = DateTime.UtcNow;
            existing.dueDate = dueDate;
            existing.status = 1;
            existing.remark = content;
            if (!string.IsNullOrWhiteSpace(savedFilePath))
            {
                existing.file_path = savedFilePath;
            }
        }

        _db.SaveChanges();
        StatusMessage = $"{GetReportTitle(reportType, reportNo)} submitted successfully.";
        return RedirectToPage();
    }

    public string GetStatusLabel(byte? status)
    {
        return status switch
        {
            1 => "Submitted",
            2 => "Approved",
            3 => "Rejected",
            _ => "Pending"
        };
    }

    private void LoadReports(int applicationId)
    {
        Reports = _db.ProgressReports.AsNoTracking()
            .Where(r => r.applicantId == applicationId)
            .OrderBy(r => r.reportType == "final" ? 1 : 0)
            .ThenBy(r => r.reportNo ?? 99)
            .ThenBy(r => r.updated_at)
            .ToList();
    }

    public string GetReportTitle(string type, byte? no)
    {
        if (string.Equals(type, "final", StringComparison.OrdinalIgnoreCase))
        {
            return "Final Report";
        }

        return no.HasValue ? $"Progress Report {no.Value}" : "Progress Report";
    }

    private bool TryParseReportKey(string key, out string reportType, out byte? reportNo)
    {
        reportType = "progress";
        reportNo = null;

        var raw = (key ?? string.Empty).Trim().ToUpperInvariant();
        if (raw == "F")
        {
            reportType = "final";
            return true;
        }

        if (raw.StartsWith("P") && byte.TryParse(raw[1..], out var no) && no >= 1 && no <= 6)
        {
            reportType = "progress";
            reportNo = no;
            return true;
        }

        return false;
    }

    private bool TryGetApprovedStudent(out StudentApplication student)
    {
        student = null!;

        var role = HttpContext.Session.GetString("UserRole");
        if (!string.Equals(role, "student", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var userIdRaw = HttpContext.Session.GetString("UserID");
        if (!int.TryParse(userIdRaw, out var userId))
        {
            return false;
        }

        var user = _db.SysUsers.AsNoTracking().FirstOrDefault(u => u.user_id == userId);
        if (user?.application_id == null)
        {
            return false;
        }

        var app = _db.StudentApplications.AsNoTracking()
            .Include(s => s.Cohort)
            .FirstOrDefault(s => s.application_id == user.application_id.Value);
        if (app == null)
        {
            return false;
        }

        if (!string.Equals(app.applyStatus?.Trim(), "Approved", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "Progress report submission is available only when your status is Approved.";
            return false;
        }

        student = app;
        return true;
    }

    private void BuildReportTypes(Cohort? cohort)
    {
        ReportTypes = new List<ReportOptionItem>
        {
            new() { Key = "P1", Label = "Progress Report 1", DueDate = cohort?.report1DueDate },
            new() { Key = "P2", Label = "Progress Report 2", DueDate = cohort?.report2DueDate },
            new() { Key = "P3", Label = "Progress Report 3", DueDate = cohort?.report3DueDate },
            new() { Key = "P4", Label = "Progress Report 4", DueDate = cohort?.report4DueDate },
            new() { Key = "P5", Label = "Progress Report 5", DueDate = cohort?.report5DueDate },
            new() { Key = "P6", Label = "Progress Report 6", DueDate = cohort?.report6DueDate },
            new() { Key = "F", Label = "Final Report", DueDate = cohort?.finalReportDueDate }
        };
    }

    private DateTime? GetDueDate(string reportType, byte? reportNo, Cohort? cohort)
    {
        if (cohort == null)
        {
            return null;
        }

        if (string.Equals(reportType, "final", StringComparison.OrdinalIgnoreCase))
        {
            return cohort.finalReportDueDate;
        }

        return reportNo switch
        {
            1 => cohort.report1DueDate,
            2 => cohort.report2DueDate,
            3 => cohort.report3DueDate,
            4 => cohort.report4DueDate,
            5 => cohort.report5DueDate,
            6 => cohort.report6DueDate,
            _ => null
        };
    }

    private void ValidateReportFile(IFormFile? file)
    {
        if (file == null)
        {
            return;
        }

        if (file.Length <= 0)
        {
            ModelState.AddModelError(nameof(Input.ReportFile), "Uploaded file is empty.");
            return;
        }

        if (file.Length > MaxUploadBytes)
        {
            ModelState.AddModelError(nameof(Input.ReportFile), "File must not exceed 10MB.");
            return;
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".doc", ".docx" };
        if (!allowed.Contains(ext))
        {
            ModelState.AddModelError(nameof(Input.ReportFile), "Only .pdf, .doc, .docx files are allowed.");
        }
    }

    private string? SaveReportFile(IFormFile? file)
    {
        if (file == null || file.Length <= 0)
        {
            return null;
        }

        var uploadPath = Path.Combine(_env.WebRootPath, "uploads", "reports");
        Directory.CreateDirectory(uploadPath);

        var ext = Path.GetExtension(file.FileName);
        var fileName = $"report_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploadPath, fileName);

        using var stream = new FileStream(fullPath, FileMode.Create);
        file.CopyTo(stream);

        return $"/uploads/reports/{fileName}";
    }
}
