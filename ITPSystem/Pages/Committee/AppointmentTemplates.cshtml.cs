using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

public class CommitteeAppointmentTemplatesModel : CommitteePageModelBase
{
    private const long MaxUploadBytes = 15 * 1024 * 1024;
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;

    public CommitteeAppointmentTemplatesModel(ApplicationDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public List<CohortOption> CohortOptions { get; private set; } = new();
    public List<TemplateListItem> Templates { get; private set; } = new();

    [BindProperty]
    public TemplateInputModel Input { get; set; } = new();

    [BindProperty] public IFormFile? CompanyAcceptanceLetterFile { get; set; }
    [BindProperty] public IFormFile? IndemnityLetterFile { get; set; }
    [BindProperty] public IFormFile? ParentAcknowledgementFormFile { get; set; }
    [BindProperty] public IFormFile? CompanySupervisorEvaluationFormFile { get; set; }
    [BindProperty] public IFormFile? ProgressReportTemplateFile { get; set; }
    [BindProperty] public IFormFile? FinalReportTemplateFile { get; set; }
    [BindProperty] public IFormFile? StudentSupportLetterFile { get; set; }
    [BindProperty] public IFormFile? AppointmentConfirmationLetterFile { get; set; }
    [BindProperty] public IFormFile? WarningLetterFile { get; set; }

    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string? ErrorMessage { get; set; }

    public class CohortOption
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class TemplateListItem
    {
        public AppointmentLetterTemplate Template { get; set; } = null!;
        public string CohortLabel { get; set; } = string.Empty;
    }

    public class TemplateInputModel
    {
        public int? TemplateId { get; set; }

        [Required(ErrorMessage = "Please choose a cohort.")]
        public int? CohortId { get; set; }

        [Required(ErrorMessage = "Please enter a template name.")]
        [StringLength(100)]
        public string? TemplateName { get; set; }

        public string? Content { get; set; }
        public string? ExistingCompanyAcceptanceLetterPath { get; set; }
        public string? ExistingIndemnityLetterPath { get; set; }
        public string? ExistingParentAcknowledgementFormPath { get; set; }
        public string? ExistingCompanySupervisorEvaluationFormPath { get; set; }
        public string? ExistingProgressReportTemplatePath { get; set; }
        public string? ExistingFinalReportTemplatePath { get; set; }
        public string? ExistingStudentSupportLetterPath { get; set; }
        public string? ExistingAppointmentConfirmationLetterPath { get; set; }
        public string? ExistingWarningLetterPath { get; set; }
    }

    public IActionResult OnGet(int? editId = null)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        LoadPageData();

        if (!editId.HasValue)
        {
            return Page();
        }

        var template = _db.AppointmentLetterTemplates
            .AsNoTracking()
            .FirstOrDefault(x => x.template_id == editId.Value);

        if (template == null)
        {
            ErrorMessage = "Selected template record was not found.";
            return RedirectToPage();
        }

        Input = MapToInput(template);
        return Page();
    }

    public IActionResult OnPostSave()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        ValidateUploads();
        ValidateTemplateContent();

        if (!ModelState.IsValid)
        {
            LoadPageData();
            return Page();
        }

        var cohortId = Input.CohortId.GetValueOrDefault();
        var cohort = _db.Cohorts.AsNoTracking().FirstOrDefault(c => c.cohort_id == cohortId);
        if (cohort == null)
        {
            ModelState.AddModelError(nameof(Input.CohortId), "Selected cohort was not found.");
            LoadPageData();
            return Page();
        }

        var isEdit = Input.TemplateId.HasValue;
        AppointmentLetterTemplate template;

        if (isEdit)
        {
            var templateId = Input.TemplateId.GetValueOrDefault();
            template = _db.AppointmentLetterTemplates.FirstOrDefault(x => x.template_id == templateId)!;
            if (template == null)
            {
                ErrorMessage = "Selected template record was not found.";
                return RedirectToPage();
            }
        }
        else
        {
            template = new AppointmentLetterTemplate();
            _db.AppointmentLetterTemplates.Add(template);
        }

        template.cohort_id = cohortId;
        template.template_name = Clean(Input.TemplateName);
        template.content = Clean(Input.Content);

        var templateFolder = EnsureTemplateFolder(cohort);

        template.company_acceptance_letter_path = SaveTemplateFile(CompanyAcceptanceLetterFile, Input.ExistingCompanyAcceptanceLetterPath, templateFolder, "company-acceptance");
        template.indemnity_letter_path = SaveTemplateFile(IndemnityLetterFile, Input.ExistingIndemnityLetterPath, templateFolder, "indemnity-letter");
        template.parent_acknowledgement_form_path = SaveTemplateFile(ParentAcknowledgementFormFile, Input.ExistingParentAcknowledgementFormPath, templateFolder, "parent-acknowledgement");
        template.company_supervisor_evaluation_form_path = SaveTemplateFile(CompanySupervisorEvaluationFormFile, Input.ExistingCompanySupervisorEvaluationFormPath, templateFolder, "company-supervisor-evaluation");
        template.progress_report_template_path = SaveTemplateFile(ProgressReportTemplateFile, Input.ExistingProgressReportTemplatePath, templateFolder, "progress-report");
        template.final_report_template_path = SaveTemplateFile(FinalReportTemplateFile, Input.ExistingFinalReportTemplatePath, templateFolder, "final-report");
        template.student_support_letter_path = SaveTemplateFile(StudentSupportLetterFile, Input.ExistingStudentSupportLetterPath, templateFolder, "student-support");
        template.appointment_confirmation_letter_path = SaveTemplateFile(AppointmentConfirmationLetterFile, Input.ExistingAppointmentConfirmationLetterPath, templateFolder, "appointment-confirmation");
        template.warning_letter_path = SaveTemplateFile(WarningLetterFile, Input.ExistingWarningLetterPath, templateFolder, "warning-letter");

        _db.SaveChanges();

        StatusMessage = isEdit
            ? $"Template set \"{template.template_name}\" updated successfully."
            : $"Template set \"{template.template_name}\" uploaded successfully.";

        return RedirectToPage();
    }

    public IActionResult OnPostDelete(int id)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var template = _db.AppointmentLetterTemplates.FirstOrDefault(x => x.template_id == id);
        if (template == null)
        {
            ErrorMessage = "Selected template record was not found.";
            return RedirectToPage();
        }

        DeleteStoredFile(template.company_acceptance_letter_path);
        DeleteStoredFile(template.indemnity_letter_path);
        DeleteStoredFile(template.parent_acknowledgement_form_path);
        DeleteStoredFile(template.company_supervisor_evaluation_form_path);
        DeleteStoredFile(template.progress_report_template_path);
        DeleteStoredFile(template.final_report_template_path);
        DeleteStoredFile(template.student_support_letter_path);
        DeleteStoredFile(template.appointment_confirmation_letter_path);
        DeleteStoredFile(template.warning_letter_path);

        _db.AppointmentLetterTemplates.Remove(template);
        _db.SaveChanges();

        StatusMessage = "Template set deleted successfully.";
        return RedirectToPage();
    }

    private void LoadPageData()
    {
        CohortOptions = _db.Cohorts.AsNoTracking()
            .OrderByDescending(c => c.startDate)
            .ThenByDescending(c => c.cohort_id)
            .Select(c => new CohortOption
            {
                Id = c.cohort_id,
                Label = BuildCohortLabel(c)
            })
            .ToList();

        Templates = _db.AppointmentLetterTemplates
            .AsNoTracking()
            .Include(x => x.Cohort)
            .OrderByDescending(x => x.created_at)
            .ThenByDescending(x => x.template_id)
            .Select(x => new TemplateListItem
            {
                Template = x,
                CohortLabel = x.Cohort != null ? BuildCohortLabel(x.Cohort) : $"Cohort {x.cohort_id}"
            })
            .ToList();
    }

    private TemplateInputModel MapToInput(AppointmentLetterTemplate template)
    {
        return new TemplateInputModel
        {
            TemplateId = template.template_id,
            CohortId = template.cohort_id,
            TemplateName = template.template_name,
            Content = template.content,
            ExistingCompanyAcceptanceLetterPath = template.company_acceptance_letter_path,
            ExistingIndemnityLetterPath = template.indemnity_letter_path,
            ExistingParentAcknowledgementFormPath = template.parent_acknowledgement_form_path,
            ExistingCompanySupervisorEvaluationFormPath = template.company_supervisor_evaluation_form_path,
            ExistingProgressReportTemplatePath = template.progress_report_template_path,
            ExistingFinalReportTemplatePath = template.final_report_template_path,
            ExistingStudentSupportLetterPath = template.student_support_letter_path,
            ExistingAppointmentConfirmationLetterPath = template.appointment_confirmation_letter_path,
            ExistingWarningLetterPath = template.warning_letter_path
        };
    }

    private void ValidateUploads()
    {
        ValidateUpload(CompanyAcceptanceLetterFile, "Company Acceptance Letter");
        ValidateUpload(IndemnityLetterFile, "Indemnity Letter");
        ValidateUpload(ParentAcknowledgementFormFile, "Parent Acknowledgement Form");
        ValidateUpload(CompanySupervisorEvaluationFormFile, "Company Supervisor Evaluation Form");
        ValidateUpload(ProgressReportTemplateFile, "Progress Report Template");
        ValidateUpload(FinalReportTemplateFile, "Final Report Template");
        ValidateUpload(StudentSupportLetterFile, "Student Support Letter");
        ValidateUpload(AppointmentConfirmationLetterFile, "Appointment Confirmation Letter");
        ValidateUpload(WarningLetterFile, "Warning Letter");
    }

    private void ValidateUpload(IFormFile? file, string label)
    {
        if (file == null)
        {
            return;
        }

        if (file.Length == 0)
        {
            ModelState.AddModelError(string.Empty, $"{label}: uploaded file is empty.");
        }

        if (file.Length > MaxUploadBytes)
        {
            ModelState.AddModelError(string.Empty, $"{label}: file size must be 15 MB or smaller.");
        }
    }

    private void ValidateTemplateContent()
    {
        var hasAnyContent = !string.IsNullOrWhiteSpace(Input.Content)
            || CompanyAcceptanceLetterFile != null
            || IndemnityLetterFile != null
            || ParentAcknowledgementFormFile != null
            || CompanySupervisorEvaluationFormFile != null
            || ProgressReportTemplateFile != null
            || FinalReportTemplateFile != null
            || StudentSupportLetterFile != null
            || AppointmentConfirmationLetterFile != null
            || WarningLetterFile != null
            || !string.IsNullOrWhiteSpace(Input.ExistingCompanyAcceptanceLetterPath)
            || !string.IsNullOrWhiteSpace(Input.ExistingIndemnityLetterPath)
            || !string.IsNullOrWhiteSpace(Input.ExistingParentAcknowledgementFormPath)
            || !string.IsNullOrWhiteSpace(Input.ExistingCompanySupervisorEvaluationFormPath)
            || !string.IsNullOrWhiteSpace(Input.ExistingProgressReportTemplatePath)
            || !string.IsNullOrWhiteSpace(Input.ExistingFinalReportTemplatePath)
            || !string.IsNullOrWhiteSpace(Input.ExistingStudentSupportLetterPath)
            || !string.IsNullOrWhiteSpace(Input.ExistingAppointmentConfirmationLetterPath)
            || !string.IsNullOrWhiteSpace(Input.ExistingWarningLetterPath);

        if (!hasAnyContent)
        {
            ModelState.AddModelError(string.Empty, "Please add template content or upload at least one template file.");
        }
    }

    private string EnsureTemplateFolder(Cohort cohort)
    {
        var cohortFolder = BuildTemplateCohortFolderName(cohort);
        var templateFolder = Path.Combine(_env.WebRootPath, "uploads", "templates", cohortFolder);
        Directory.CreateDirectory(templateFolder);
        return templateFolder;
    }

    private string? SaveTemplateFile(IFormFile? file, string? existingPath, string folderPath, string prefix)
    {
        if (file == null || file.Length == 0)
        {
            return string.IsNullOrWhiteSpace(existingPath) ? null : existingPath;
        }

        var safeName = BuildSafeUploadFileName(file.FileName, prefix);
        var fullPath = Path.Combine(folderPath, safeName);

        var oldPath = ResolveUploadFullPath(existingPath);
        if (!string.IsNullOrWhiteSpace(oldPath) && System.IO.File.Exists(oldPath))
        {
            try
            {
                System.IO.File.Delete(oldPath);
            }
            catch
            {
                // keep save success even if old cleanup fails
            }
        }

        using var stream = new FileStream(fullPath, FileMode.Create);
        file.CopyTo(stream);

        var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads");
        var relativeFolder = Path.GetRelativePath(uploadsRoot, folderPath).Replace("\\", "/");
        return $"/uploads/{relativeFolder}/{safeName}";
    }

    private void DeleteStoredFile(string? storedPath)
    {
        var fullPath = ResolveUploadFullPath(storedPath);
        if (string.IsNullOrWhiteSpace(fullPath) || !System.IO.File.Exists(fullPath))
        {
            return;
        }

        try
        {
            System.IO.File.Delete(fullPath);
        }
        catch
        {
            // ignore cleanup failure on delete
        }
    }

    private string? ResolveUploadFullPath(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath) || string.IsNullOrWhiteSpace(_env.WebRootPath))
        {
            return null;
        }

        var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads");
        var normalizedPath = storedPath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        if (normalizedPath.StartsWith($"uploads{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            normalizedPath = normalizedPath.Substring($"uploads{Path.DirectorySeparatorChar}".Length);
        }

        var combinedPath = Path.GetFullPath(Path.Combine(uploadsRoot, normalizedPath));
        var uploadsRootFullPath = Path.GetFullPath(uploadsRoot);
        return combinedPath.StartsWith(uploadsRootFullPath, StringComparison.OrdinalIgnoreCase)
            ? combinedPath
            : null;
    }

    private static string BuildSafeUploadFileName(string? originalFileName, string fallbackPrefix)
    {
        var extension = Path.GetExtension(originalFileName);
        var safeExtension = string.IsNullOrWhiteSpace(extension) ? ".bin" : extension;
        var baseName = Path.GetFileNameWithoutExtension(originalFileName);

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = fallbackPrefix;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitizedBaseName = new string(baseName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        sanitizedBaseName = sanitizedBaseName.Replace(" ", "-");

        return $"{fallbackPrefix}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{sanitizedBaseName}{safeExtension}";
    }

    private static string BuildCohortLabel(Cohort cohort)
    {
        var description = string.IsNullOrWhiteSpace(cohort.description)
            ? $"Cohort {cohort.cohort_id}"
            : cohort.description.Trim();

        return $"{description} (ID {cohort.cohort_id})";
    }

    private static string BuildCohortFolderName(Cohort cohort)
    {
        var rawName = string.IsNullOrWhiteSpace(cohort.description)
            ? $"Cohort_{cohort.cohort_id}"
            : cohort.description.Trim();

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(rawName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        sanitized = string.Join("_", sanitized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

        return string.IsNullOrWhiteSpace(sanitized) ? $"Cohort_{cohort.cohort_id}" : sanitized;
    }

    private static string BuildTemplateCohortFolderName(Cohort cohort)
    {
        return $"cohort{cohort.cohort_id}";
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
