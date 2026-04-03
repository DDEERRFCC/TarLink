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
    private const string DynamicCompanyAcceptanceFile = CompanyAcceptanceLetterWordTemplateBuilder.GeneratedFileName;
    private const string DynamicIndemnityFile = IndemnityLetterWordTemplateBuilder.GeneratedFileName;
    private const string DynamicParentAcknowledgementFile = ParentAcknowledgementFormWordTemplateBuilder.GeneratedFileName;
    private const string DynamicStudentSupportLetterFile = StudentSupportLetterWordTemplateBuilder.GeneratedFileName;
    private const string CompanySupervisorEvaluationTemplateFile = "FOCS_EmpF03.xlsx";
    private const string ProgressReportTemplateFile = "FOCS_studF03 Progress Report Template.docx";
    private const string FinalReportTemplateFile = "FOCS_studF04 Final Report Template.docx";
    private const string AppointmentConfirmationLetterFile = "DownloadAppointmentLetter.docx";
    private const string ApprovedCompanySupervisorEvaluationFile = "FOCS_EmpF03.xlsx";
    private const string WarningLetterFile = "WarningLetter.docx";
    private const string IndemnityDisplayTitle = "Indemnity Letter";
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
            DynamicIndemnityFile,
            DynamicParentAcknowledgementFile,
            DynamicStudentSupportLetterFile,
            CompanySupervisorEvaluationTemplateFile,
            ProgressReportTemplateFile,
            FinalReportTemplateFile,
            AppointmentConfirmationLetterFile,
            ApprovedCompanySupervisorEvaluationFile,
            WarningLetterFile
        };

        var safeFileName = Path.GetFileName(file ?? string.Empty);
        if (string.IsNullOrWhiteSpace(safeFileName) || !allowedFiles.Contains(safeFileName))
        {
            return NotFound();
        }

        if (string.Equals(safeFileName, DynamicIndemnityFile, StringComparison.OrdinalIgnoreCase))
        {
            var student = GetCurrentStudentApplication(includeCohort: true);
            var fileBytes = IndemnityLetterWordTemplateBuilder.Build(student, _env.WebRootPath);
            const string indemnityContentType = "application/pdf";
            if (download)
            {
                return File(fileBytes, indemnityContentType, DynamicIndemnityFile);
            }

            return File(fileBytes, indemnityContentType);
        }

        if (string.Equals(safeFileName, DynamicCompanyAcceptanceFile, StringComparison.OrdinalIgnoreCase))
        {
            var student = GetCurrentStudentApplication(includeCohort: true);
            var fileBytes = CompanyAcceptanceLetterWordTemplateBuilder.Build(student, _env.WebRootPath);
            const string companyAcceptanceContentType = "application/pdf";
            if (download)
            {
                return File(fileBytes, companyAcceptanceContentType, DynamicCompanyAcceptanceFile);
            }

            return File(fileBytes, companyAcceptanceContentType);
        }

        if (string.Equals(safeFileName, DynamicParentAcknowledgementFile, StringComparison.OrdinalIgnoreCase))
        {
            var student = GetCurrentStudentApplication(includeCohort: true);
            var fileBytes = ParentAcknowledgementFormWordTemplateBuilder.Build(student, _env.WebRootPath);
            const string parentAcknowledgementContentType = "application/pdf";
            if (download)
            {
                return File(fileBytes, parentAcknowledgementContentType, DynamicParentAcknowledgementFile);
            }

            return File(fileBytes, parentAcknowledgementContentType);
        }

        if (string.Equals(safeFileName, DynamicStudentSupportLetterFile, StringComparison.OrdinalIgnoreCase))
        {
            var student = GetCurrentStudentApplication(includeCohort: false);
            var fileBytes = StudentSupportLetterWordTemplateBuilder.Build(student, _env.WebRootPath);
            const string studentSupportLetterContentType = "application/pdf";
            if (download)
            {
                return File(fileBytes, studentSupportLetterContentType, DynamicStudentSupportLetterFile);
            }

            return File(fileBytes, studentSupportLetterContentType);
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
            (IndemnityDisplayTitle, DynamicIndemnityFile, true),
            ("Parent Acknowledgement Form", DynamicParentAcknowledgementFile, true),
            ("Company Supervisor Evaluation Form", CompanySupervisorEvaluationTemplateFile, false),
            ("Progress Report Template", ProgressReportTemplateFile, false),
            ("Final Report Template", FinalReportTemplateFile, false),
            ("Student Support Letter", DynamicStudentSupportLetterFile, true)
        };

        Documents = requiredDocs
            .Select(d => new DocumentItem
            {
                Title = d.Title,
                ViewPath = Url.Page("/Student/Documents", "FormDocument", new { file = d.FileName, download = false }) ?? "#",
                DownloadPath = Url.Page("/Student/Documents", "FormDocument", new { file = d.FileName, download = true }) ?? "#",
                Exists = IsDynamicDocument(d.FileName) || StaticDocumentExists(d.FileName),
                CanView = d.CanView
            })
            .ToList();

        ApprovedStatusDocuments = new();
        if (string.Equals(Status?.Trim(), "Approved", StringComparison.OrdinalIgnoreCase))
        {
            var approvedDocs = new (string Title, string FileName, bool CanView)[]
            {
                ("Appointment Confirmation Letter", AppointmentConfirmationLetterFile, true),
                ("Company Supervisor Evaluation Form", ApprovedCompanySupervisorEvaluationFile, true),
                ("Warning Letter", WarningLetterFile, true)
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
        return string.Equals(fileName, DynamicIndemnityFile, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, DynamicCompanyAcceptanceFile, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, DynamicParentAcknowledgementFile, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, DynamicStudentSupportLetterFile, StringComparison.OrdinalIgnoreCase);
    }

    private bool StaticDocumentExists(string fileName)
    {
        var directory = Path.Combine(_env.WebRootPath, "documents", "templates");
        return System.IO.File.Exists(Path.Combine(directory, fileName));
    }
}
