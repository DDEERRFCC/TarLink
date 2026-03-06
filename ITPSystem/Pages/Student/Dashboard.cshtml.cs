using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;

public class StudentDashboardModel : PageModel
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;
    private readonly IWebHostEnvironment _env;
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public StudentDashboardModel(IWebHostEnvironment env, ApplicationDbContext db, IConfiguration config)
    {
        _env = env;
        _db = db;
        _config = config;
    }

    public List<DocumentItem> Documents { get; private set; } = new();
    public List<DocumentItem> ApprovedStatusDocuments { get; private set; } = new();
    public List<CompanyOptionItem> CompanyOptions { get; private set; } = new();
    public List<Announcement> Announcements { get; private set; } = new();
    public List<DeadlineItem> ReportDeadlines { get; private set; } = new();
    public string Cohort { get; private set; } = "-";
    public string InternPeriod { get; private set; } = "-";
    public string Status { get; private set; } = "-";
    public string Remark { get; private set; } = "-";
    public string InternshipStatusText { get; private set; } = "Internship timeline is not available.";
    public int InternshipProgressPercent { get; private set; }
    public int InternshipCurrentWeek { get; private set; }
    public int InternshipTotalWeeks { get; private set; }
    private string ExistingCompanyName { get; set; } = string.Empty;
    public string CurrentFormAcceptanceFile { get; private set; } = "-";
    public string CurrentFormAcknowledgementFile { get; private set; } = "-";
    public string CurrentLetterIdentityFile { get; private set; } = "-";
    public string CurrentOtherEvidenceFile { get; private set; } = "-";

    [BindProperty]
    public CompanyDetailInput Input { get; set; } = new();

    public class DocumentItem
    {
        public string Title { get; set; } = string.Empty;
        public string ViewPath { get; set; } = string.Empty;
        public string DownloadPath { get; set; } = string.Empty;
        public bool Exists { get; set; }
        public bool CanView { get; set; } = true;
    }

    public class CompanyOptionItem
    {
        public int CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<string> Addresses { get; set; } = new();
    }

    public class DeadlineItem
    {
        public string Title { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    public class CompanyDetailInput
    {
        public int? CompanyId { get; set; }
        public string? Address { get; set; }

        [Range(0, 1000000, ErrorMessage = "Monthly Allowance must be 0 or above.")]
        public decimal? MonthlyAllowance { get; set; }

        public string? CompanySupervisorName { get; set; }

        [EmailAddress(ErrorMessage = "Please enter a valid Company Supervisor Email.")]
        public string? CompanySupervisorEmail { get; set; }

        public IFormFile? FormAcceptanceFile { get; set; }
        public IFormFile? FormAcknowledgementFile { get; set; }
        public IFormFile? LetterOfIndemnityFile { get; set; }
        public IFormFile? HiredEvidenceFile { get; set; }
    }

    public IActionResult OnGet()
    {
        var role = HttpContext.Session.GetString("UserRole");
        if (!string.Equals(role, "student", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToPage("/Login/StudentLogin");
        }

        LoadStudentDashboardInfo(setInputFromDb: true);
        LoadCompanyOptions();
        MapExistingCompanySelection();
        LoadDocuments();
        LoadAnnouncements();

        return Page();
    }

    public IActionResult OnPostSaveCompany()
    {
        var role = HttpContext.Session.GetString("UserRole");
        if (!string.Equals(role, "student", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToPage("/Login/StudentLogin");
        }

        var student = GetCurrentStudentApplication(asNoTracking: false, includeCohort: false);
        if (student == null)
        {
            ModelState.AddModelError("", "No linked student application found for this account.");
            LoadCompanyOptions();
            LoadDocuments();
            LoadAnnouncements();
            return Page();
        }

        TryValidateModel(Input, nameof(Input));

        LoadCompanyOptions();
        var selectedCompany = Input.CompanyId.HasValue
            ? CompanyOptions.FirstOrDefault(c => c.CompanyId == Input.CompanyId.Value)
            : null;

        if (selectedCompany == null)
        {
            ModelState.AddModelError("", "Please select a company.");
        }

        var selectedAddress = (Input.Address ?? string.Empty).Trim();
        if (selectedCompany != null && (string.IsNullOrWhiteSpace(selectedAddress) || !selectedCompany.Addresses.Contains(selectedAddress)))
        {
            ModelState.AddModelError("", "Please select a valid address for the selected company.");
        }

        ValidateUpload(Input.FormAcceptanceFile, "Com. Acceptance Form");
        ValidateUpload(Input.FormAcknowledgementFile, "Parent Ack. Form");
        ValidateUpload(Input.LetterOfIndemnityFile, "Letter of Indemnity");
        ValidateUpload(Input.HiredEvidenceFile, "Hired evidence");

        if (!ModelState.IsValid || selectedCompany == null)
        {
            LoadStudentDashboardInfo(setInputFromDb: false);
            LoadDocuments();
            LoadAnnouncements();
            return Page();
        }

        student.comName = selectedCompany.Name;
        student.comAddress = selectedAddress;
        student.allowance = Input.MonthlyAllowance;
        student.comSupervisor = string.IsNullOrWhiteSpace(Input.CompanySupervisorName) ? null : Input.CompanySupervisorName.Trim();
        student.comSupervisorEmail = string.IsNullOrWhiteSpace(Input.CompanySupervisorEmail) ? null : Input.CompanySupervisorEmail.Trim();
        student.formAcceptance = SaveUploadedFile(Input.FormAcceptanceFile, "formAcceptance", student.formAcceptance);
        student.formAcknowledgement = SaveUploadedFile(Input.FormAcknowledgementFile, "formAcknowledgement", student.formAcknowledgement);
        student.letterIdentity = SaveUploadedFile(Input.LetterOfIndemnityFile, "letterIdentity", student.letterIdentity);
        student.otherEvidence = SaveUploadedFile(Input.HiredEvidenceFile, "otherEvidence", student.otherEvidence);
        student.updated_at = DateTime.Now;

        _db.SaveChanges();
        var emailSent = TrySendSupervisorEmail(student);
        TempData["SuccessMessage"] = emailSent
            ? "Company details updated successfully. Notification email sent to supervisor."
            : "Company details updated successfully.";
        return RedirectToPage();
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
            "DownloadCompanyAcceptanceLetter.pdf",
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

    private void LoadDocuments()
    {
        var formDir = Path.Combine(_env.WebRootPath, "documents", "templates");
        var requiredDocs = new (string Title, string FileName, bool CanView)[]
        {
            ("Company Acceptance Letter", "DownloadCompanyAcceptanceLetter.pdf", true),
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
                ViewPath = Url.Page("/Student/Dashboard", "FormDocument", new { file = d.FileName, download = false }) ?? "#",
                DownloadPath = Url.Page("/Student/Dashboard", "FormDocument", new { file = d.FileName, download = true }) ?? "#",
                Exists = System.IO.File.Exists(Path.Combine(formDir, d.FileName)),
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
                    ViewPath = Url.Page("/Student/Dashboard", "FormDocument", new { file = d.FileName, download = false }) ?? "#",
                    DownloadPath = Url.Page("/Student/Dashboard", "FormDocument", new { file = d.FileName, download = true }) ?? "#",
                    Exists = System.IO.File.Exists(Path.Combine(formDir, d.FileName)),
                    CanView = d.CanView
                })
                .ToList();
        }
    }

    private void LoadStudentDashboardInfo(bool setInputFromDb)
    {
        var student = GetCurrentStudentApplication(asNoTracking: true, includeCohort: true);

        if (student == null)
        {
            return;
        }

        Cohort = string.IsNullOrWhiteSpace(student.Cohort?.description)
            ? student.cohortId.ToString()
            : student.Cohort.description;
        var startDate = student.Cohort?.startDate?.ToString("yyyy-MM-dd") ?? "-";
        var endDate = student.Cohort?.endDate?.ToString("yyyy-MM-dd") ?? "-";
        InternPeriod = $"{startDate} to {endDate}";

        Status = string.IsNullOrWhiteSpace(student.applyStatus) ? "-" : student.applyStatus;
        Remark = string.IsNullOrWhiteSpace(student.remark) ? "-" : student.remark;
        CalculateInternshipProgress(student.Cohort, Status);

        CurrentFormAcceptanceFile = string.IsNullOrWhiteSpace(student.formAcceptance) ? "-" : student.formAcceptance;
        CurrentFormAcknowledgementFile = string.IsNullOrWhiteSpace(student.formAcknowledgement) ? "-" : student.formAcknowledgement;
        CurrentLetterIdentityFile = string.IsNullOrWhiteSpace(student.letterIdentity) ? "-" : student.letterIdentity;
        CurrentOtherEvidenceFile = string.IsNullOrWhiteSpace(student.otherEvidence) ? "-" : student.otherEvidence;
        BuildReportDeadlines(student.Cohort);

        if (setInputFromDb)
        {
            ExistingCompanyName = student.comName ?? string.Empty;
            Input.Address = student.comAddress;
            Input.MonthlyAllowance = student.allowance;
            Input.CompanySupervisorName = student.comSupervisor;
            Input.CompanySupervisorEmail = student.comSupervisorEmail;
        }
    }

    private void CalculateInternshipProgress(Cohort? cohort, string? applyStatus)
    {
        InternshipProgressPercent = 0;
        InternshipCurrentWeek = 0;
        InternshipTotalWeeks = 0;

        var normalizedStatus = (applyStatus ?? string.Empty).Trim().ToLowerInvariant();
        if (cohort?.startDate == null || cohort.endDate == null)
        {
            InternshipStatusText = normalizedStatus == "approved"
                ? "Application approved. Internship timeline is not configured yet."
                : $"Application status: {(string.IsNullOrWhiteSpace(applyStatus) ? "-" : applyStatus)}.";
            return;
        }

        var start = cohort.startDate.Value.Date;
        var end = cohort.endDate.Value.Date;
        if (end < start)
        {
            InternshipStatusText = "Internship timeline is invalid. Please contact the committee.";
            return;
        }

        var totalDays = (end - start).Days + 1;
        InternshipTotalWeeks = Math.Max(1, (int)Math.Ceiling(totalDays / 7.0));

        var today = DateTime.Today;
        if (today < start)
        {
            InternshipCurrentWeek = 0;
            InternshipProgressPercent = 0;
        }
        else if (today > end)
        {
            InternshipCurrentWeek = InternshipTotalWeeks;
            InternshipProgressPercent = 100;
        }
        else
        {
            var elapsedDays = (today - start).Days + 1;
            InternshipCurrentWeek = Math.Clamp((int)Math.Ceiling(elapsedDays / 7.0), 1, InternshipTotalWeeks);
            InternshipProgressPercent = Math.Clamp((int)Math.Round((elapsedDays * 100.0) / totalDays), 0, 100);
        }

        InternshipStatusText = normalizedStatus switch
        {
            "approved" when today < start => "Application approved. Internship has not started yet.",
            "approved" when today > end => "Application approved. Internship period completed.",
            "approved" => "Application approved and currently in progress.",
            "pending" => "Application is pending approval.",
            "rejected" => "Application was rejected.",
            "withdrawn" => "Application was withdrawn.",
            _ => $"Application status: {(string.IsNullOrWhiteSpace(applyStatus) ? "-" : applyStatus)}."
        };
    }

    private void BuildReportDeadlines(Cohort? cohort)
    {
        ReportDeadlines = new();
        if (cohort == null)
        {
            return;
        }

        void AddProgressDeadline(int no, DateTime? dueDate, string? monthName)
        {
            if (!dueDate.HasValue)
            {
                return;
            }

            var monthPart = string.IsNullOrWhiteSpace(monthName) ? string.Empty : $" ({monthName.Trim()})";
            ReportDeadlines.Add(new DeadlineItem
            {
                Title = $"Progress Report {no}{monthPart}",
                Date = dueDate.Value,
                Note = "Submit your report before due date."
            });
        }

        AddProgressDeadline(1, cohort.report1DueDate, cohort.reportMonth1);
        AddProgressDeadline(2, cohort.report2DueDate, cohort.reportMonth2);
        AddProgressDeadline(3, cohort.report3DueDate, cohort.reportMonth3);
        AddProgressDeadline(4, cohort.report4DueDate, cohort.reportMonth4);
        AddProgressDeadline(5, cohort.report5DueDate, cohort.reportMonth5);
        AddProgressDeadline(6, cohort.report6DueDate, cohort.reportMonth6);

        if (cohort.finalReportDueDate.HasValue)
        {
            ReportDeadlines.Add(new DeadlineItem
            {
                Title = "Final Report",
                Date = cohort.finalReportDueDate.Value,
                Note = "Submit your final report draft."
            });
        }

        ReportDeadlines = ReportDeadlines
            .OrderBy(d => d.Date)
            .ToList();
    }

    private void LoadCompanyOptions()
    {
        var companies = _db.Companies.AsNoTracking()
            .Where(c => (c.status ?? 1) == 1 && (c.visibility ?? 1) == 1)
            .OrderBy(c => c.name)
            .Select(c => new
            {
                c.company_id,
                c.name,
                c.address1,
                c.address2,
                c.address3
            })
            .ToList();

        CompanyOptions = companies
            .Select(c => new CompanyOptionItem
            {
                CompanyId = c.company_id,
                Name = c.name,
                Addresses = new[] { c.address1, c.address2, c.address3 }
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Select(a => a!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .Where(c => c.Addresses.Count > 0)
            .ToList();
    }

    private void MapExistingCompanySelection()
    {
        if (Input.CompanyId.HasValue || string.IsNullOrWhiteSpace(ExistingCompanyName))
        {
            return;
        }

        var existing = CompanyOptions.FirstOrDefault(c => string.Equals(c.Name, ExistingCompanyName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            Input.CompanyId = existing.CompanyId;
        }
    }

    private void ValidateUpload(IFormFile? file, string label)
    {
        if (file == null)
        {
            return;
        }

        if (file.Length <= 0)
        {
            ModelState.AddModelError("", $"{label}: uploaded file is empty.");
            return;
        }

        if (file.Length > MaxUploadBytes)
        {
            ModelState.AddModelError("", $"{label}: file must not exceed 10MB.");
        }
    }

    private string? SaveUploadedFile(IFormFile? file, string prefix, string? existingFileName)
    {
        if (file == null || file.Length <= 0)
        {
            return existingFileName;
        }

        var uploadsPath = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsPath);

        var extension = Path.GetExtension(file.FileName);
        var safeExt = string.IsNullOrWhiteSpace(extension) ? string.Empty : extension;
        var savedName = $"{prefix}_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{safeExt}";
        var fullPath = Path.Combine(uploadsPath, savedName);

        using var stream = new FileStream(fullPath, FileMode.Create);
        file.CopyTo(stream);

        return savedName;
    }

    private StudentApplication? GetCurrentStudentApplication(bool asNoTracking, bool includeCohort)
    {
        var userIdText = HttpContext.Session.GetString("UserID");
        if (!int.TryParse(userIdText, out var userId))
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

    private bool TrySendSupervisorEmail(StudentApplication student)
    {
        var recipients = new[] { student.ucSupervisorEmail, student.comSupervisorEmail }
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (recipients.Count == 0)
        {
            return false;
        }

        var host = _config["Email:SmtpHost"];
        var portText = _config["Email:SmtpPort"];
        var user = _config["Email:SmtpUser"];
        var pass = _config["Email:SmtpPass"];
        var from = _config["Email:From"];
        var useSslText = _config["Email:UseSsl"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from) || !int.TryParse(portText, out var port))
        {
            return false;
        }

        var useSsl = true;
        if (!string.IsNullOrWhiteSpace(useSslText) && bool.TryParse(useSslText, out var parsedSsl))
        {
            useSsl = parsedSsl;
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(from),
                Subject = "Student Company Details Updated",
                Body = $"Student {student.studentName} ({student.studentID}) updated company details.\n" +
                       $"Company: {student.comName}\n" +
                       $"Address: {student.comAddress}\n" +
                       $"Allowance: {student.allowance}\n" +
                       $"Company Supervisor: {student.comSupervisor}\n" +
                       $"Company Supervisor Email: {student.comSupervisorEmail}\n" +
                       $"Updated At: {student.updated_at:yyyy-MM-dd HH:mm:ss}"
            };

            foreach (var recipient in recipients)
            {
                message.To.Add(recipient);
            }

            using var smtp = new SmtpClient(host, port)
            {
                EnableSsl = useSsl
            };

            if (!string.IsNullOrWhiteSpace(user))
            {
                smtp.Credentials = new NetworkCredential(user, pass ?? string.Empty);
            }

            smtp.Send(message);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void LoadAnnouncements()
    {
        var now = DateTime.Now;

        Announcements = _db.Announcements.AsNoTracking()
            .Where(a => a.is_published)
            .Where(a => a.target_role == "all" || a.target_role == "student")
            .Where(a => !a.publish_at.HasValue || a.publish_at.Value <= now)
            .Where(a => !a.expire_at.HasValue || a.expire_at.Value >= now)
            .OrderByDescending(a => a.publish_at ?? a.created_at)
            .ThenByDescending(a => a.created_at)
            .ToList();
    }
}

