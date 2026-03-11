using ITPSystem.Data;
using ITPSystem.Helpers;
using ITPSystem.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

public class StudentDocumentsModel : PageModel
{
    private const string DynamicCompanyAcceptanceFile = CompanyAcceptanceLetterDocumentBuilder.GeneratedFileName;
    private readonly IWebHostEnvironment _env;
    private readonly ApplicationDbContext _db;

    public StudentDocumentsModel(IWebHostEnvironment env, ApplicationDbContext db)
    {
        _env = env;
        _db = db;
    }

    public List<DocumentItem> Documents { get; private set; } = new();
    public List<DocumentItem> ApprovedStatusDocuments { get; private set; } = new();
    public string Status { get; private set; } = "-";

    public class DocumentItem
    {
        public string Title { get; set; } = string.Empty;
        public string ViewPath { get; set; } = string.Empty;
        public string DownloadPath { get; set; } = string.Empty;
        public bool Exists { get; set; }
        public bool CanView { get; set; } = true;
    }

    public IActionResult OnGet()
    {
        var role = HttpContext.Session.GetString("UserRole");
        if (!string.Equals(role, "student", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToPage("/Login/StudentLogin");
        }

        LoadStudentStatus();
        LoadDocuments();
        return Page();
    }

    public IActionResult OnGetFormDocument(string file, bool download = false)
    {
        var role = HttpContext.Session.GetString("UserRole");
        if (!string.Equals(role, "student", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToPage("/Login/StudentLogin");
        }

        var allowedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            DynamicCompanyAcceptanceFile,
            "DownloadIndemnityLetter.pdf",
            "DownloadParentAcknowledgementForm.pdf",
            "CompanySupervisorEvaluationForm.xlsx",
            "ProgressReportTemplate.docx",
            "FinalReportTemplate.docx",
            "StudentSupportLetter.pdf",
            "AppointmentConfirmationLetter.pdf",
            "CompanySupervisorEvaluationForm.pdf",
            "WarningLetter.pdf"
        };

        var safeFileName = Path.GetFileName(file ?? string.Empty);
        if (string.IsNullOrWhiteSpace(safeFileName) || !allowedFiles.Contains(safeFileName))
        {
            return NotFound();
        }

        if (string.Equals(safeFileName, DynamicCompanyAcceptanceFile, StringComparison.OrdinalIgnoreCase))
        {
            var student = GetCurrentStudentApplication(includeCohort: true);
            var fileBytes = CompanyAcceptanceLetterDocumentBuilder.BuildPdf(student);
            if (download)
            {
                return File(fileBytes, "application/pdf", DynamicCompanyAcceptanceFile);
            }

            return File(fileBytes, "application/pdf");
        }

        var formDir = Path.Combine(_env.WebRootPath, "documents", "templates");
        var fullPath = Path.Combine(formDir, safeFileName);
        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(safeFileName, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        if (download)
        {
            return PhysicalFile(fullPath, contentType, safeFileName);
        }

        return PhysicalFile(fullPath, contentType);
    }

    private void LoadStudentStatus()
    {
        var student = GetCurrentStudentApplication();
        if (student != null && !string.IsNullOrWhiteSpace(student.applyStatus))
        {
            Status = student.applyStatus;
        }
    }

    private void LoadDocuments()
    {
        var formDir = Path.Combine(_env.WebRootPath, "documents", "templates");
        var requiredDocs = new (string Title, string FileName, bool CanView)[]
        {
            ("Company Acceptance Letter", DynamicCompanyAcceptanceFile, true),
            ("Indemnity Letter", "DownloadIndemnityLetter.pdf", true),
            ("Parent Acknowledgement Form", "DownloadParentAcknowledgementForm.pdf", true),
            ("Company Supervisor Evaluation Form", "CompanySupervisorEvaluationForm.xlsx", false),
            ("Progress Report Template", "ProgressReportTemplate.docx", false),
            ("Final Report Template", "FinalReportTemplate.docx", false),
            ("Student Support Letter", "StudentSupportLetter.pdf", true)
        };

        Documents = requiredDocs
            .Select(d => new DocumentItem
            {
                Title = d.Title,
                ViewPath = Url.Page("/Student/Documents", "FormDocument", new { file = d.FileName, download = false }) ?? "#",
                DownloadPath = Url.Page("/Student/Documents", "FormDocument", new { file = d.FileName, download = true }) ?? "#",
                Exists = IsDynamicDocument(d.FileName) || System.IO.File.Exists(Path.Combine(formDir, d.FileName)),
                CanView = d.CanView
            })
            .ToList();

        ApprovedStatusDocuments = new();
        if (string.Equals(Status?.Trim(), "Approved", StringComparison.OrdinalIgnoreCase))
        {
            var approvedDocs = new (string Title, string FileName, bool CanView)[]
            {
                ("Appointment Confirmation Letter", "AppointmentConfirmationLetter.pdf", true),
                ("Company Supervisor Evaluation Form", "CompanySupervisorEvaluationForm.pdf", true),
                ("Warning Letter", "WarningLetter.pdf", true)
            };

            ApprovedStatusDocuments = approvedDocs
                .Select(d => new DocumentItem
                {
                    Title = d.Title,
                    ViewPath = Url.Page("/Student/Documents", "FormDocument", new { file = d.FileName, download = false }) ?? "#",
                    DownloadPath = Url.Page("/Student/Documents", "FormDocument", new { file = d.FileName, download = true }) ?? "#",
                    Exists = System.IO.File.Exists(Path.Combine(formDir, d.FileName)),
                    CanView = d.CanView
                })
                .ToList();
        }
    }

    private StudentApplication? GetCurrentStudentApplication()
    {
        return GetCurrentStudentApplication(includeCohort: false);
    }

    private StudentApplication? GetCurrentStudentApplication(bool includeCohort)
    {
        var userIdText = HttpContext.Session.GetString("UserID");
        if (!int.TryParse(userIdText, out var userId))
        {
            return null;
        }

        var user = _db.SysUsers.AsNoTracking().FirstOrDefault(u => u.user_id == userId);
        if (user?.application_id == null)
        {
            return null;
        }

        var query = _db.StudentApplications.AsNoTracking();
        if (includeCohort)
        {
            query = query.Include(s => s.Cohort);
        }

        return query
            .FirstOrDefault(s => s.application_id == user.application_id.Value);
    }

    private static bool IsDynamicDocument(string fileName)
    {
        return string.Equals(fileName, DynamicCompanyAcceptanceFile, StringComparison.OrdinalIgnoreCase);
    }
}
