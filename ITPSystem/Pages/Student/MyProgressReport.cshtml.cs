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
    private const byte SubmittedStatus = 1;
    private const byte ApprovedStatus = 2;
    private const byte RejectedStatus = 3;
    private const byte SubmittedLateStatus = 4;
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

        var savedFilePath = SaveReportFile(Input.ReportFile);

        var dueDate = GetDueDate(reportType, reportNo, student.Cohort) ?? DateTime.UtcNow.Date;
        var submissionStatus = IsLateSubmission(dueDate) ? SubmittedLateStatus : SubmittedStatus;
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
                status = submissionStatus,
                remark = null,
                file_path = savedFilePath
            });
        }
        else
        {
            existing.updated_at = DateTime.UtcNow;
            existing.dueDate = dueDate;
            existing.status = submissionStatus;
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
            SubmittedStatus => "Submitted",
            ApprovedStatus => "Approved",
            RejectedStatus => "Rejected",
            SubmittedLateStatus => "Submitted Late",
            _ => "Pending"
        };
    }

    public string GetDisplayStatus(ProgressReport? report, DateTime? dueDate)
    {
        if (report != null)
        {
            return GetStatusLabel(report.status);
        }

        return dueDate.HasValue && DateTime.Today > dueDate.Value.Date
            ? "Missing"
            : "Pending";
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

    public ProgressReport? GetReportForKey(string key)
    {
        if (!TryParseReportKey(key, out var reportType, out var reportNo))
        {
            return null;
        }

        return Reports.FirstOrDefault(r =>
            string.Equals(r.reportType, reportType, StringComparison.OrdinalIgnoreCase) &&
            r.reportNo == reportNo);
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

    private static bool IsLateSubmission(DateTime dueDate)
    {
        return DateTime.Today > dueDate.Date;
    }

    private void ValidateReportFile(IFormFile? file)
    {
        if (file == null)
        {
            ModelState.AddModelError(nameof(Input.ReportFile), "Please upload a report file.");
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

        var student = GetCurrentStudentApplication(asNoTracking: true, includeCohort: true);
        if (student == null)
        {
            return null;
        }

        var uploadPath = EnsureStudentReportFolder(student);

        var fileName = BuildSafeUploadFileName(file.FileName, "report");
        var fullPath = Path.Combine(uploadPath, fileName);

        using var stream = new FileStream(fullPath, FileMode.Create);
        file.CopyTo(stream);

        var relativeFolder = Path.GetRelativePath(Path.Combine(_env.WebRootPath, "uploads"), uploadPath)
            .Replace('\\', '/');
        return $"/uploads/{relativeFolder}/{fileName}";
    }

    private string EnsureStudentReportFolder(StudentApplication student)
    {
        var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsRoot);

        var cohortFolderName = BuildSafeFolderName(student.Cohort?.description, $"Cohort_{student.cohortId}");
        var cohortFolderPath = Path.Combine(uploadsRoot, "Cohorts", cohortFolderName);
        Directory.CreateDirectory(cohortFolderPath);

        var studentFolderName = BuildSafeFolderName(student.studentName, $"Student_{student.application_id}");
        var studentFolderPath = Path.Combine(cohortFolderPath, studentFolderName);
        Directory.CreateDirectory(studentFolderPath);

        var reportFolderPath = Path.Combine(studentFolderPath, "Report");
        Directory.CreateDirectory(reportFolderPath);

        return reportFolderPath;
    }

    private static string BuildSafeFolderName(string? rawValue, string fallback)
    {
        var baseValue = string.IsNullOrWhiteSpace(rawValue) ? fallback : rawValue.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(baseValue
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray());

        sanitized = string.Join("_", sanitized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }

    private static string BuildSafeUploadFileName(string? originalFileName, string fallbackPrefix)
    {
        var rawFileName = Path.GetFileName(originalFileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(rawFileName))
        {
            return fallbackPrefix;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(rawFileName
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray())
            .Trim();

        return string.IsNullOrWhiteSpace(sanitized) ? fallbackPrefix : sanitized;
    }

    private StudentApplication? GetCurrentStudentApplication(bool asNoTracking, bool includeCohort)
    {
        var userIdRaw = HttpContext.Session.GetString("UserID");
        if (!int.TryParse(userIdRaw, out var userId))
        {
            return null;
        }

        var userQuery = asNoTracking ? _db.SysUsers.AsNoTracking() : _db.SysUsers;
        var user = userQuery.FirstOrDefault(u => u.user_id == userId);
        if (user?.application_id == null)
        {
            return null;
        }

        IQueryable<StudentApplication> studentQuery = asNoTracking ? _db.StudentApplications.AsNoTracking() : _db.StudentApplications;
        if (includeCohort)
        {
            studentQuery = studentQuery.Include(s => s.Cohort);
        }

        return studentQuery.FirstOrDefault(s => s.application_id == user.application_id.Value);
    }
}
