using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using ITPSystem.Data;
using ITPSystem.Helpers;
using ITPSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using ITPSystem.Services;

public class StudentDashboardModel : PageModel
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;
    private readonly IWebHostEnvironment _env;
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly OllamaStudentAssistantService _assistantService;

    public StudentDashboardModel(IWebHostEnvironment env, ApplicationDbContext db, IConfiguration config, OllamaStudentAssistantService assistantService)
    {
        _env = env;
        _db = db;
        _config = config;
        _assistantService = assistantService;
    }

    public List<DocumentItem> Documents { get; private set; } = new();
    public List<DocumentItem> ApprovedStatusDocuments { get; private set; } = new();
    public List<CompanyOptionItem> CompanyOptions { get; private set; } = new();
    public List<Announcement> Announcements { get; private set; } = new();
    public List<Notification> RecentNotifications { get; private set; } = new();
    public List<DeadlineItem> ReportDeadlines { get; private set; } = new();
    public int UnreadNotificationCount { get; private set; }
    public int BellItemCount => Announcements.Count + UnreadNotificationCount;
    public string Cohort { get; private set; } = "-";
    public string InternPeriod { get; private set; } = "-";
    public string Status { get; private set; } = "None";
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
    public string TemplateSourceLabel { get; private set; } = "-";
    public bool CanEditCompanyDetail { get; private set; } = true;
    public bool CanEditCompanySelection { get; private set; } = true;
    public bool CanEditCompanyUploads { get; private set; } = true;
    public bool CanEditCompanyAllowanceAndSupervisor { get; private set; } = true;
    public bool IsApprovedCompanyDetailLimitedEdit { get; private set; }
    public string CompanyDetailActionLabel { get; private set; } = "Submit Company Details";
    public bool CanViewProgressReport { get; private set; }

    [BindProperty]
    public CompanyDetailInput Input { get; set; } = new();

    public class DocumentItem
    {
        public string Key { get; set; } = string.Empty;
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
        public List<CompanySupervisorOption> Supervisors { get; set; } = new();
    }

    public class CompanySupervisorOption
    {
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
    }

    public class DeadlineItem
    {
        public string Title { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Note { get; set; } = string.Empty;
        public bool IsOverdue { get; set; }
    }

    public class CompanyDetailInput
    {
        [Required(ErrorMessage = "Please select a company.")]
        public int? CompanyId { get; set; }

        [Required(ErrorMessage = "Please select an address.")]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Please enter monthly allowance.")]
        [Range(0, 1000000, ErrorMessage = "Monthly Allowance must be 0 or above.")]
        public decimal? MonthlyAllowance { get; set; }

        [Required(ErrorMessage = "Please select a company supervisor.")]
        public string? CompanySupervisorName { get; set; }

        [Required(ErrorMessage = "Please provide the company supervisor email.")]
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
        LoadNotifications();

        return Page();
    }

    public IActionResult OnPostSaveCompany()
    {
        var role = HttpContext.Session.GetString("UserRole");
        if (!string.Equals(role, "student", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToPage("/Login/StudentLogin");
        }

        var student = GetCurrentStudentApplication(asNoTracking: false, includeCohort: true);
        if (student == null)
        {
            ModelState.AddModelError("", "No linked student application found for this account.");
            LoadCompanyOptions();
            LoadDocuments();
            LoadAnnouncements();
            LoadNotifications();
            return Page();
        }

        if (!CanEditCompanyDetailForStatus(student.applyStatus))
        {
            TempData["ErrorMessage"] = "Company details cannot be edited for the current application status.";
            return RedirectToPage();
        }

        TryValidateModel(Input, nameof(Input));

        LoadCompanyOptions();
        var canEditSelection = CanEditCompanySelectionForStatus(student.applyStatus);
        var isApprovedLimitedEdit = IsApprovedLimitedCompanyEditStatus(student.applyStatus);
        CompanyOptionItem? selectedCompany;

        if (canEditSelection)
        {
            selectedCompany = Input.CompanyId.HasValue
                ? CompanyOptions.FirstOrDefault(c => c.CompanyId == Input.CompanyId.Value)
                : null;
        }
        else
        {
            selectedCompany = CompanyOptions.FirstOrDefault(c =>
                string.Equals(c.Name, student.comName, StringComparison.OrdinalIgnoreCase));
            Input.CompanyId = selectedCompany?.CompanyId;
            Input.Address = student.comAddress;
        }

        if (canEditSelection && selectedCompany == null)
        {
            ModelState.AddModelError("", "Please select a company.");
        }

        var selectedAddress = (Input.Address ?? string.Empty).Trim();
        if (canEditSelection && selectedCompany != null && (string.IsNullOrWhiteSpace(selectedAddress) || !selectedCompany.Addresses.Contains(selectedAddress)))
        {
            ModelState.AddModelError("", "Please select a valid address for the selected company.");
        }

        Input.CompanySupervisorName = string.IsNullOrWhiteSpace(Input.CompanySupervisorName)
            ? null
            : Input.CompanySupervisorName.Trim();
        Input.CompanySupervisorEmail = string.IsNullOrWhiteSpace(Input.CompanySupervisorEmail)
            ? null
            : Input.CompanySupervisorEmail.Trim();

        if (canEditSelection)
        {
            ValidateUpload(Input.FormAcceptanceFile, "Com. Acceptance Form");
            ValidateUpload(Input.FormAcknowledgementFile, "Parent Ack. Form");
            ValidateUpload(Input.LetterOfIndemnityFile, "Letter of Indemnity");
            ValidateUpload(Input.HiredEvidenceFile, "Hired evidence");
            RequireUploadOrExistingFile(Input.FormAcceptanceFile, student.formAcceptance, "Com. Acceptance Form");
            RequireUploadOrExistingFile(Input.FormAcknowledgementFile, student.formAcknowledgement, "Parent Ack. Form");
            RequireUploadOrExistingFile(Input.LetterOfIndemnityFile, student.letterIdentity, "Letter of Indemnity");
        }

        if (!ModelState.IsValid || (canEditSelection && selectedCompany == null))
        {
            LoadStudentDashboardInfo(setInputFromDb: false);
            LoadDocuments();
            LoadAnnouncements();
            LoadNotifications();
            return Page();
        }

        student.allowance = Input.MonthlyAllowance;
        student.comSupervisor = string.IsNullOrWhiteSpace(Input.CompanySupervisorName) ? null : Input.CompanySupervisorName.Trim();
        student.comSupervisorEmail = string.IsNullOrWhiteSpace(Input.CompanySupervisorEmail) ? null : Input.CompanySupervisorEmail.Trim();

        if (canEditSelection && selectedCompany != null)
        {
            student.comName = selectedCompany.Name;
            student.comAddress = selectedAddress;
            student.formAcceptance = SaveUploadedFile(Input.FormAcceptanceFile, "formAcceptance", student.formAcceptance, student);
            student.formAcknowledgement = SaveUploadedFile(Input.FormAcknowledgementFile, "formAcknowledgement", student.formAcknowledgement, student);
            student.letterIdentity = SaveUploadedFile(Input.LetterOfIndemnityFile, "letterIdentity", student.letterIdentity, student);
            student.otherEvidence = SaveUploadedFile(Input.HiredEvidenceFile, "otherEvidence", student.otherEvidence, student);
            student.applyStatus = "pending";
        }

        student.updated_at = DateTime.Now;

        _db.SaveChanges();

        if (isApprovedLimitedEdit)
        {
            TempData["SuccessMessage"] = "Company details updated successfully.";
            return RedirectToPage();
        }

        var emailSent = TrySendSupervisorEmail(student);
        TempData["SuccessMessage"] = emailSent
            ? "Company details submitted successfully. Status is now pending and notification email was sent to supervisor."
            : "Company details submitted successfully. Status is now pending.";
        return RedirectToPage();
    }

    public IActionResult OnPostMarkNotificationsRead()
    {
        var role = HttpContext.Session.GetString("UserRole");
        if (!string.Equals(role, "student", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonResult(new { success = false });
        }

        var userIdText = HttpContext.Session.GetString("UserID");
        if (!int.TryParse(userIdText, out var userId))
        {
            return new JsonResult(new { success = false });
        }

        var unreadNotifications = _db.Notifications
            .Where(n => n.to_user_id == userId && !n.is_read)
            .ToList();

        if (unreadNotifications.Count > 0)
        {
            foreach (var item in unreadNotifications)
            {
                item.is_read = true;
            }

            _db.SaveChanges();
        }

        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostAskAssistantAsync([FromForm] string question, CancellationToken cancellationToken)
    {
        var role = HttpContext.Session.GetString("UserRole");
        if (!string.Equals(role, "student", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonResult(new { success = false, answer = "Please log in as a student first." });
        }

        var trimmedQuestion = (question ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmedQuestion))
        {
            return new JsonResult(new { success = false, answer = "Please type a question first." });
        }

        var student = GetCurrentStudentApplication(asNoTracking: true, includeCohort: true);
        var context = BuildAssistantContext(student);
        var answer = await _assistantService.AskAsync(context, trimmedQuestion, cancellationToken);

        return new JsonResult(new { success = true, answer });
    }

    public IActionResult OnGetFormDocument(string file, bool download = false)
    {
        var role = HttpContext.Session.GetString("UserRole");
        if (!string.Equals(role, "student", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToPage("/Login/StudentLogin");
        }

        var key = (file ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return NotFound();
        }

        var student = GetCurrentStudentApplication(asNoTracking: true, includeCohort: true);
        if (student == null)
        {
            return NotFound();
        }

        var template = GetTemplateForStudent(student);
        if (template == null)
        {
            return NotFound();
        }

        var storedPath = GetTemplatePath(template, key);
        var fullPath = ResolveUploadFullPath(storedPath);
        if (string.IsNullOrWhiteSpace(fullPath) || !System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        var generatedPdfFileName = BuildGeneratedPdfFileName(fullPath);

        if (string.Equals(key, "company_acceptance_letter", StringComparison.OrdinalIgnoreCase))
        {
            var fileBytes = CompanyAcceptanceLetterWordTemplateBuilder.BuildFromTemplatePath(student, fullPath);
            const string pdfContentTypeCompanyAcceptance = "application/pdf";
            return download
                ? File(fileBytes, pdfContentTypeCompanyAcceptance, generatedPdfFileName)
                : File(fileBytes, pdfContentTypeCompanyAcceptance);
        }

        if (string.Equals(key, "indemnity_letter", StringComparison.OrdinalIgnoreCase))
        {
            var fileBytes = IndemnityLetterWordTemplateBuilder.BuildFromTemplatePath(student, fullPath);
            const string pdfContentTypeIndemnity = "application/pdf";
            return download
                ? File(fileBytes, pdfContentTypeIndemnity, generatedPdfFileName)
                : File(fileBytes, pdfContentTypeIndemnity);
        }

        if (string.Equals(key, "parent_acknowledgement_form", StringComparison.OrdinalIgnoreCase))
        {
            var fileBytes = ParentAcknowledgementFormWordTemplateBuilder.BuildFromTemplatePath(student, fullPath);
            const string pdfContentTypeParentAcknowledgement = "application/pdf";
            return download
                ? File(fileBytes, pdfContentTypeParentAcknowledgement, generatedPdfFileName)
                : File(fileBytes, pdfContentTypeParentAcknowledgement);
        }

        if (string.Equals(key, "student_support_letter", StringComparison.OrdinalIgnoreCase))
        {
            var fileBytes = StudentSupportLetterWordTemplateBuilder.BuildFromTemplatePath(student, fullPath);
            const string pdfContentTypeStudentSupport = "application/pdf";
            return download
                ? File(fileBytes, pdfContentTypeStudentSupport, generatedPdfFileName)
                : File(fileBytes, pdfContentTypeStudentSupport);
        }

        if (string.Equals(key, "appointment_confirmation_letter", StringComparison.OrdinalIgnoreCase))
        {
            var fileBytes = AppointmentConfirmationLetterWordTemplateBuilder.BuildFromTemplatePath(student, fullPath);
            const string pdfContentTypeAppointmentConfirmation = "application/pdf";
            return download
                ? File(fileBytes, pdfContentTypeAppointmentConfirmation, generatedPdfFileName)
                : File(fileBytes, pdfContentTypeAppointmentConfirmation);
        }

        var safeFileName = Path.GetFileName(fullPath);

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

    private static string BuildGeneratedPdfFileName(string fullPath)
    {
        var originalName = Path.GetFileNameWithoutExtension(fullPath);
        return string.IsNullOrWhiteSpace(originalName)
            ? "document.pdf"
            : $"{originalName}.pdf";
    }

    private void LoadDocuments()
    {
        var student = GetCurrentStudentApplication(asNoTracking: true, includeCohort: true);
        var template = student != null
            ? GetTemplateForStudent(student)
            : null;

        TemplateSourceLabel = template?.template_name
            ?? (student?.Cohort?.description is { Length: > 0 } description
                ? $"{description.Trim()} template"
                : "No cohort template");

        var requiredDocs = new (string Key, string Title, string? StoredPath, bool CanView)[]
        {
            ("indemnity_letter", "Indemnity Letter", template?.indemnity_letter_path, true),
            ("company_acceptance_letter", "Company Acceptance Letter", template?.company_acceptance_letter_path, true),
            ("parent_acknowledgement_form", "Parent Acknowledgement Form", template?.parent_acknowledgement_form_path, true),
            ("company_supervisor_evaluation_form", "Company Supervisor Evaluation Form", template?.company_supervisor_evaluation_form_path, false),
            ("progress_report_template", "Progress Report Template", template?.progress_report_template_path, false),
            ("final_report_template", "Final Report Template", template?.final_report_template_path, false),
            ("student_support_letter", "Student Support Letter", template?.student_support_letter_path, true)
        };

        Documents = requiredDocs
            .Select(d => new DocumentItem
            {
                Key = d.Key,
                Title = d.Title,
                ViewPath = BuildDocumentPath(d.Key, false),
                DownloadPath = BuildDocumentPath(d.Key, true),
                Exists = TemplateDocumentExists(d.StoredPath),
                CanView = d.CanView
            })
            .ToList();

        ApprovedStatusDocuments = new();
        if (string.Equals(Status?.Trim(), "Approved", StringComparison.OrdinalIgnoreCase))
        {
            var approvedDocs = new (string Key, string Title, string? StoredPath, bool CanView)[]
            {
                ("appointment_confirmation_letter", "Appointment Confirmation Letter", template?.appointment_confirmation_letter_path, true),
                ("company_supervisor_evaluation_form", "Company Supervisor Evaluation Form", template?.company_supervisor_evaluation_form_path, false),
                ("warning_letter", "Warning Letter", template?.warning_letter_path, false)
            };

            ApprovedStatusDocuments = approvedDocs
                .Select(d => new DocumentItem
                {
                    Key = d.Key,
                    Title = d.Title,
                    ViewPath = BuildDocumentPath(d.Key, false),
                    DownloadPath = BuildDocumentPath(d.Key, true),
                    Exists = TemplateDocumentExists(d.StoredPath),
                    CanView = d.CanView
                })
                .ToList();
        }
    }

    private string BuildDocumentPath(string key, bool download)
    {
        return Url.Page("/Student/Dashboard", "FormDocument", new { file = key, download }) ?? "#";
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

        Status = string.IsNullOrWhiteSpace(student.applyStatus) ? "None" : student.applyStatus;
        Remark = string.IsNullOrWhiteSpace(student.remark) ? "-" : student.remark;
        CanEditCompanyDetail = CanEditCompanyDetailForStatus(student.applyStatus);
        CanEditCompanySelection = CanEditCompanySelectionForStatus(student.applyStatus);
        CanEditCompanyUploads = CanEditCompanySelection;
        CanEditCompanyAllowanceAndSupervisor = CanEditCompanyDetail;
        IsApprovedCompanyDetailLimitedEdit = IsApprovedLimitedCompanyEditStatus(student.applyStatus);
        CompanyDetailActionLabel = GetCompanyDetailActionLabel(student.applyStatus);
        CanViewProgressReport = string.Equals(Status?.Trim(), "Approved", StringComparison.OrdinalIgnoreCase);
        CalculateInternshipProgress(student.Cohort, Status);

        CurrentFormAcceptanceFile = BuildCurrentFileDisplay(student.formAcceptance);
        CurrentFormAcknowledgementFile = BuildCurrentFileDisplay(student.formAcknowledgement);
        CurrentLetterIdentityFile = BuildCurrentFileDisplay(student.letterIdentity);
        CurrentOtherEvidenceFile = BuildCurrentFileDisplay(student.otherEvidence);
        BuildReportDeadlines(student);

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
                : $"Application status: {(string.IsNullOrWhiteSpace(applyStatus) ? "None" : applyStatus)}.";
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
            _ => $"Application status: {(string.IsNullOrWhiteSpace(applyStatus) ? "None" : applyStatus)}."
        };
    }

    private void BuildReportDeadlines(StudentApplication student)
    {
        ReportDeadlines = new();
        var cohort = student.Cohort;
        if (cohort == null)
        {
            return;
        }

        var submittedReportKeys = _db.ProgressReports.AsNoTracking()
            .Where(r => r.applicantId == student.application_id)
            .Select(r => new { r.reportType, r.reportNo })
            .ToList()
            .Select(r => string.Equals(r.reportType, "final", StringComparison.OrdinalIgnoreCase)
                ? "F"
                : $"P{r.reportNo}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var today = DateTime.Today;

        void AddProgressDeadline(int no, DateTime? dueDate, string? monthName)
        {
            if (!dueDate.HasValue)
            {
                return;
            }

            var reportKey = $"P{no}";
            if (submittedReportKeys.Contains(reportKey))
            {
                return;
            }

            var monthPart = string.IsNullOrWhiteSpace(monthName) ? string.Empty : $" ({monthName.Trim()})";
            ReportDeadlines.Add(new DeadlineItem
            {
                Title = $"Progress Report {no}{monthPart}",
                Date = dueDate.Value,
                Note = dueDate.Value.Date < today ? "Overdue. Please submit as soon as possible." : "Submit your report before due date.",
                IsOverdue = dueDate.Value.Date < today
            });
        }

        AddProgressDeadline(1, cohort.report1DueDate, cohort.reportMonth1);
        AddProgressDeadline(2, cohort.report2DueDate, cohort.reportMonth2);
        AddProgressDeadline(3, cohort.report3DueDate, cohort.reportMonth3);
        AddProgressDeadline(4, cohort.report4DueDate, cohort.reportMonth4);
        AddProgressDeadline(5, cohort.report5DueDate, cohort.reportMonth5);
        AddProgressDeadline(6, cohort.report6DueDate, cohort.reportMonth6);

        if (cohort.finalReportDueDate.HasValue && !submittedReportKeys.Contains("F"))
        {
            ReportDeadlines.Add(new DeadlineItem
            {
                Title = "Final Report",
                Date = cohort.finalReportDueDate.Value,
                Note = cohort.finalReportDueDate.Value.Date < today ? "Overdue. Please submit as soon as possible." : "Submit your final report draft.",
                IsOverdue = cohort.finalReportDueDate.Value.Date < today
            });
        }

        ReportDeadlines = ReportDeadlines
            .OrderBy(d => d.Date)
            .ToList();
    }

    private void LoadCompanyOptions()
    {
        var companies = _db.Companies.AsNoTracking()
            .Include(c => c.Branches)
            .Where(c => c.Branches.Any(b => (b.status ?? 0) == 1 && (b.visibility ?? 1) == 1))
            .OrderBy(c => c.name)
            .ToList();

        CompanyOptions = companies
            .Select(c => new CompanyOptionItem
            {
                CompanyId = c.company_id,
                Name = c.name,
                Addresses = c.Branches
                    .Where(a => (a.status ?? 0) == 1 && (a.visibility ?? 1) == 1)
                    .OrderByDescending(a => a.is_hq)
                    .ThenBy(a => a.branch_id)
                    .Select(FormatBranchAddress)
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Supervisors = new List<CompanySupervisorOption>()
            })
            .Where(c => c.Addresses.Count > 0)
            .ToList();
    }

    private static string FormatBranchAddress(CompanyBranch branch)
    {
        var parts = new[]
        {
            branch.address_line,
            branch.city,
            branch.state,
            branch.postcode,
            branch.country
        };

        return string.Join(", ", parts
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim()));
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

    private void LoadNotifications()
    {
        var userIdText = HttpContext.Session.GetString("UserID");
        if (!int.TryParse(userIdText, out var userId))
        {
            RecentNotifications = new();
            UnreadNotificationCount = 0;
            return;
        }

        RecentNotifications = _db.Notifications.AsNoTracking()
            .Where(n => n.to_user_id == userId)
            .OrderByDescending(n => n.created_at)
            .Take(8)
            .ToList();

        UnreadNotificationCount = _db.Notifications.Count(n => n.to_user_id == userId && !n.is_read);
    }

    private StudentAssistantContext BuildAssistantContext(StudentApplication? student)
    {
        var context = new StudentAssistantContext
        {
            StudentName = student?.studentName ?? (HttpContext.Session.GetString("UserName") ?? "Student"),
            StudentId = HttpContext.Session.GetString("StudentID") ?? (student?.studentID ?? "-"),
            Status = string.IsNullOrWhiteSpace(student?.applyStatus) ? "None" : student!.applyStatus!,
            Cohort = string.IsNullOrWhiteSpace(student?.Cohort?.description) ? (student?.cohortId.ToString() ?? "-") : student!.Cohort!.description!,
            InternPeriod = student?.Cohort?.startDate != null && student.Cohort.endDate != null
                ? $"{student.Cohort.startDate:yyyy-MM-dd} to {student.Cohort.endDate:yyyy-MM-dd}"
                : "-"
        };

        if (student?.Cohort != null)
        {
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

            AddDeadline("P1", "Progress Report 1", student.Cohort.report1DueDate);
            AddDeadline("P2", "Progress Report 2", student.Cohort.report2DueDate);
            AddDeadline("P3", "Progress Report 3", student.Cohort.report3DueDate);
            AddDeadline("P4", "Progress Report 4", student.Cohort.report4DueDate);
            AddDeadline("P5", "Progress Report 5", student.Cohort.report5DueDate);
            AddDeadline("P6", "Progress Report 6", student.Cohort.report6DueDate);
            AddDeadline("F", "Final Report", student.Cohort.finalReportDueDate);
        }

        return context;
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

    private void RequireUploadOrExistingFile(IFormFile? file, string? existingFileName, string fieldLabel)
    {
        if (file != null || !string.IsNullOrWhiteSpace(existingFileName))
        {
            return;
        }

        ModelState.AddModelError(string.Empty, $"{fieldLabel} is required.");
    }

    private string? SaveUploadedFile(IFormFile? file, string prefix, string? existingFileName, StudentApplication student)
    {
        if (file == null || file.Length <= 0)
        {
            return existingFileName;
        }

        var studentFolderPath = EnsureStudentCompanyDocumentFolder(student);

        var savedName = BuildSafeUploadFileName(file.FileName, prefix);
        var fullPath = Path.Combine(studentFolderPath, savedName);

        var existingFullPath = ResolveUploadFullPath(existingFileName);
        if (!string.IsNullOrWhiteSpace(existingFullPath) && System.IO.File.Exists(existingFullPath))
        {
            try
            {
                System.IO.File.Delete(existingFullPath);
            }
            catch
            {
                // keep upload success even if cleanup fails
            }
        }

        using var stream = new FileStream(fullPath, FileMode.Create);
        file.CopyTo(stream);

        var relativeFolder = Path.GetRelativePath(Path.Combine(_env.WebRootPath, "uploads"), studentFolderPath)
            .Replace('\\', '/');
        return $"{relativeFolder}/{savedName}";
    }

    private string BuildCurrentFileDisplay(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "-";
        }

        var safeFileName = Path.GetFileName(fileName);
        var fullPath = ResolveUploadFullPath(fileName);
        if (string.IsNullOrWhiteSpace(fullPath) || !System.IO.File.Exists(fullPath))
        {
            return safeFileName;
        }

        var fileInfo = new FileInfo(fullPath);
        return $"{safeFileName} ({FormatFileSize(fileInfo.Length)})";
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024d:0.##} KB";
        }

        return $"{bytes / (1024d * 1024d):0.##} MB";
    }

    private string EnsureStudentCompanyDocumentFolder(StudentApplication student)
    {
        var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsRoot);

        var cohortFolderName = BuildSafeFolderName(student.Cohort?.description, $"Cohort_{student.cohortId}");
        var cohortFolderPath = Path.Combine(uploadsRoot, "Cohorts", cohortFolderName);
        Directory.CreateDirectory(cohortFolderPath);

        var studentFolderName = BuildSafeFolderName(student.studentName, $"Student_{student.application_id}");
        var studentFolderPath = Path.Combine(cohortFolderPath, studentFolderName);
        Directory.CreateDirectory(studentFolderPath);

        return studentFolderPath;
    }

    private string? ResolveUploadFullPath(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return null;
        }

        var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads");
        var normalizedPath = storedPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        normalizedPath = normalizedPath.TrimStart(Path.DirectorySeparatorChar);

        if (normalizedPath.StartsWith($"uploads{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            normalizedPath = normalizedPath.Substring($"uploads{Path.DirectorySeparatorChar}".Length);
        }

        var combinedPath = Path.GetFullPath(Path.Combine(uploadsRoot, normalizedPath));
        var uploadsRootFullPath = Path.GetFullPath(uploadsRoot);

        if (!combinedPath.StartsWith(uploadsRootFullPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return combinedPath;
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

    private static bool CanEditCompanyDetailForStatus(string? applyStatus)
    {
        var normalizedStatus = (applyStatus ?? string.Empty).Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalizedStatus)
            || normalizedStatus == "none"
            || normalizedStatus == "pending"
            || normalizedStatus == "approved"
            || normalizedStatus == "rejected";
    }

    private static bool CanEditCompanySelectionForStatus(string? applyStatus)
    {
        var normalizedStatus = (applyStatus ?? string.Empty).Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalizedStatus)
            || normalizedStatus == "none"
            || normalizedStatus == "pending"
            || normalizedStatus == "rejected";
    }

    private static bool IsApprovedLimitedCompanyEditStatus(string? applyStatus)
    {
        return string.Equals((applyStatus ?? string.Empty).Trim(), "approved", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetCompanyDetailActionLabel(string? applyStatus)
    {
        var normalizedStatus = (applyStatus ?? string.Empty).Trim().ToLowerInvariant();
        return normalizedStatus switch
        {
            "rejected" => "Resubmit Company Details",
            "approved" => "Update Company Details",
            _ => "Submit Company Details"
        };
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
        var student = GetCurrentStudentApplication(asNoTracking: true, includeCohort: true);
        var cohortId = student?.cohortId;
        var faculty = student?.Cohort?.faculty;
        var campus = student?.Cohort?.campus;

        Announcements = _db.Announcements.AsNoTracking()
            .Where(a => a.is_published)
            .Where(a => a.target_role == "all" || a.target_role == "student")
            .Where(a => !a.cohort_id.HasValue || a.cohort_id == cohortId)
            .Where(a => string.IsNullOrWhiteSpace(a.faculty) || a.faculty == faculty)
            .Where(a => string.IsNullOrWhiteSpace(a.campus) || a.campus == campus)
            .Where(a => !a.publish_at.HasValue || a.publish_at.Value <= now)
            .Where(a => !a.expire_at.HasValue || a.expire_at.Value >= now)
            .OrderByDescending(a => a.publish_at ?? a.created_at)
            .ThenByDescending(a => a.created_at)
            .ToList();
    }

    private AppointmentLetterTemplate? GetTemplateForStudent(StudentApplication student)
    {
        if (!student.templateVersion.HasValue)
        {
            return null;
        }

        var templateId = student.templateVersion.Value;
        return _db.AppointmentLetterTemplates.AsNoTracking()
            .FirstOrDefault(t => t.template_id == templateId);
    }

    private static string? GetTemplatePath(AppointmentLetterTemplate template, string key)
    {
        return key switch
        {
            "company_acceptance_letter" => template.company_acceptance_letter_path,
            "indemnity_letter" => template.indemnity_letter_path,
            "parent_acknowledgement_form" => template.parent_acknowledgement_form_path,
            "company_supervisor_evaluation_form" => template.company_supervisor_evaluation_form_path,
            "progress_report_template" => template.progress_report_template_path,
            "final_report_template" => template.final_report_template_path,
            "student_support_letter" => template.student_support_letter_path,
            "appointment_confirmation_letter" => template.appointment_confirmation_letter_path,
            "warning_letter" => template.warning_letter_path,
            _ => null
        };
    }

    private bool TemplateDocumentExists(string? storedPath)
    {
        var fullPath = ResolveUploadFullPath(storedPath);
        return !string.IsNullOrWhiteSpace(fullPath) && System.IO.File.Exists(fullPath);
    }
}

