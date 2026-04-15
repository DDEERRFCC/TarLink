using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

public class CommitteeCohortFormModel : CommitteePageModelBase
{
    private static readonly string[] AllowedFaculties =
    {
        "Faculty of Computing and Information Technology",
        "Faculty of Engineering and Technology",
        "Faculty of Business and Finance",
        "Faculty of Accountancy, Finance and Business",
        "Faculty of Social Science and Humanities",
        "Faculty of Built Environment"
    };

    private static readonly string[] AllowedCampuses =
    {
        "Kuala Lumpur Main Campus",
        "Penang Branch Campus",
        "Perak Branch Campus",
        "Johor Branch Campus",
        "Sabah Branch Campus",
        "Sarawak Branch Campus"
    };

    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;

    public CommitteeCohortFormModel(ApplicationDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public bool IsEditing => Input.CohortId.HasValue;

    public IReadOnlyList<SelectListItem> FacultyOptions { get; } = AllowedFaculties.Select(x => new SelectListItem(x, x)).ToList();
    public IReadOnlyList<SelectListItem> CampusOptions { get; } = AllowedCampuses.Select(x => new SelectListItem(x, x)).ToList();

    [BindProperty]
    public CohortInputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public class CohortInputModel
    {
        public int? CohortId { get; set; }

        [StringLength(100)]
        public string? Description { get; set; }

        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Range(1, 2, ErrorMessage = "Level must be Diploma or Degree.")]
        public byte? Level { get; set; }

        public bool IsActive { get; set; }

        [DataType(DataType.Date)]
        public DateTime? Report1DueDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? Report2DueDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? Report3DueDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? Report4DueDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? Report5DueDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? Report6DueDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? FinalReportDueDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ExamStartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ExamEndDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? CompanyEvaluationDate { get; set; }

        [StringLength(45)]
        public string? ReportMonth1 { get; set; }

        [StringLength(45)]
        public string? ReportMonth2 { get; set; }

        [StringLength(45)]
        public string? ReportMonth3 { get; set; }

        [StringLength(45)]
        public string? ReportMonth4 { get; set; }

        [StringLength(45)]
        public string? ReportMonth5 { get; set; }

        [StringLength(45)]
        public string? ReportMonth6 { get; set; }

        [StringLength(45)]
        public string? Campus { get; set; }

        [StringLength(45)]
        public string? Faculty { get; set; }

        [StringLength(250)]
        public string? PersonInCharge { get; set; }

        [StringLength(250)]
        [EmailAddress(ErrorMessage = "Please enter a valid PIC email.")]
        public string? PidEmail { get; set; }
    }

    public IActionResult OnGet(int? editId = null)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        if (editId.HasValue)
        {
            var cohort = _db.Cohorts.FirstOrDefault(c => c.cohort_id == editId.Value);
            if (cohort == null)
            {
                ErrorMessage = "Selected cohort was not found.";
                return RedirectToPage("/Committee/Cohorts");
            }

            Input = MapToInput(cohort);
        }

        return Page();
    }

    public IActionResult OnPostSave()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        ValidateCohortDates();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var isEdit = Input.CohortId.HasValue;
        Cohort cohort;

        if (isEdit)
        {
            cohort = _db.Cohorts.FirstOrDefault(c => c.cohort_id == Input.CohortId!.Value)!;
            if (cohort == null)
            {
                ErrorMessage = "Selected cohort was not found.";
                return RedirectToPage("/Committee/Cohorts");
            }
        }
        else
        {
            cohort = new Cohort();
            _db.Cohorts.Add(cohort);
        }

        ApplyInput(cohort);
        _db.SaveChanges();

        SyncCohortStudentLocks(cohort.cohort_id, cohort.isActive);

        if (!isEdit)
        {
            EnsureCohortStorageFolders(cohort);
        }

        StatusMessage = isEdit
            ? $"Cohort {cohort.cohort_id} updated successfully."
            : $"Cohort {cohort.cohort_id} created successfully with storage folders ready.";

        return RedirectToPage("/Committee/Cohorts");
    }

    private void ValidateCohortDates()
    {
        if (Input.StartDate.HasValue && Input.EndDate.HasValue && Input.EndDate.Value.Date < Input.StartDate.Value.Date)
        {
            ModelState.AddModelError(nameof(Input.EndDate), "End date must be on or after start date.");
        }

        if (Input.ExamStartDate.HasValue && Input.ExamEndDate.HasValue && Input.ExamEndDate.Value.Date < Input.ExamStartDate.Value.Date)
        {
            ModelState.AddModelError(nameof(Input.ExamEndDate), "Exam end date must be on or after exam start date.");
        }

        if (!string.IsNullOrWhiteSpace(Input.Campus) && !AllowedCampuses.Contains(Input.Campus.Trim(), StringComparer.Ordinal))
        {
            ModelState.AddModelError(nameof(Input.Campus), "Invalid campus value.");
        }

        if (!string.IsNullOrWhiteSpace(Input.Faculty) && !AllowedFaculties.Contains(Input.Faculty.Trim(), StringComparer.Ordinal))
        {
            ModelState.AddModelError(nameof(Input.Faculty), "Invalid faculty value.");
        }
    }

    private CohortInputModel MapToInput(Cohort cohort)
    {
        return new CohortInputModel
        {
            CohortId = cohort.cohort_id,
            Description = cohort.description,
            StartDate = cohort.startDate,
            EndDate = cohort.endDate,
            Level = cohort.level,
            IsActive = cohort.isActive,
            Report1DueDate = cohort.report1DueDate,
            Report2DueDate = cohort.report2DueDate,
            Report3DueDate = cohort.report3DueDate,
            Report4DueDate = cohort.report4DueDate,
            Report5DueDate = cohort.report5DueDate,
            Report6DueDate = cohort.report6DueDate,
            FinalReportDueDate = cohort.finalReportDueDate,
            ExamStartDate = cohort.examStartDate,
            ExamEndDate = cohort.examEndDate,
            CompanyEvaluationDate = cohort.companyEvaluationDate,
            ReportMonth1 = cohort.reportMonth1,
            ReportMonth2 = cohort.reportMonth2,
            ReportMonth3 = cohort.reportMonth3,
            ReportMonth4 = cohort.reportMonth4,
            ReportMonth5 = cohort.reportMonth5,
            ReportMonth6 = cohort.reportMonth6,
            Campus = cohort.campus,
            Faculty = cohort.faculty,
            PersonInCharge = cohort.personInCharge,
            PidEmail = cohort.pidEmail
        };
    }

    private void ApplyInput(Cohort cohort)
    {
        cohort.description = Clean(Input.Description);
        cohort.startDate = Input.StartDate;
        cohort.endDate = Input.EndDate;
        cohort.level = Input.Level;
        cohort.isActive = Input.IsActive;
        cohort.report1DueDate = Input.Report1DueDate;
        cohort.report2DueDate = Input.Report2DueDate;
        cohort.report3DueDate = Input.Report3DueDate;
        cohort.report4DueDate = Input.Report4DueDate;
        cohort.report5DueDate = Input.Report5DueDate;
        cohort.report6DueDate = Input.Report6DueDate;
        cohort.finalReportDueDate = Input.FinalReportDueDate;
        cohort.examStartDate = Input.ExamStartDate;
        cohort.examEndDate = Input.ExamEndDate;
        cohort.companyEvaluationDate = Input.CompanyEvaluationDate;
        cohort.reportMonth1 = Clean(Input.ReportMonth1);
        cohort.reportMonth2 = Clean(Input.ReportMonth2);
        cohort.reportMonth3 = Clean(Input.ReportMonth3);
        cohort.reportMonth4 = Clean(Input.ReportMonth4);
        cohort.reportMonth5 = Clean(Input.ReportMonth5);
        cohort.reportMonth6 = Clean(Input.ReportMonth6);
        cohort.campus = Clean(Input.Campus);
        cohort.faculty = Clean(Input.Faculty);
        cohort.personInCharge = Clean(Input.PersonInCharge);
        cohort.pidEmail = Clean(Input.PidEmail);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void SyncCohortStudentLocks(int cohortId, bool isActive)
    {
        var apps = _db.StudentApplications.AsNoTracking()
            .Where(s => s.cohortId == cohortId)
            .Select(s => new { s.application_id, s.studentEmail, s.number_ic })
            .ToList();

        if (apps.Count == 0)
        {
            return;
        }

        var appIds = apps.Select(a => a.application_id).ToHashSet();
        var emails = apps.Select(a => a.studentEmail).Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var icNumbers = apps.Select(a => a.number_ic).Where(ic => !string.IsNullOrWhiteSpace(ic)).Select(ic => ic!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var users = _db.SysUsers
            .Where(u => u.role == "student" && (
                (u.application_id.HasValue && appIds.Contains(u.application_id.Value)) ||
                (!string.IsNullOrWhiteSpace(u.email) && emails.Contains(u.email)) ||
                (!string.IsNullOrWhiteSpace(u.ic_number) && icNumbers.Contains(u.ic_number))
            ))
            .ToList();

        if (users.Count == 0)
        {
            return;
        }

        foreach (var user in users)
        {
            user.is_locked = !isActive;
        }

        _db.SaveChanges();
    }

    private void EnsureCohortStorageFolders(Cohort cohort)
    {
        var folderName = BuildCohortFolderName(cohort);
        var cohortRoot = Path.Combine(_env.WebRootPath, "uploads", "Cohorts", folderName);
        Directory.CreateDirectory(cohortRoot);
        Directory.CreateDirectory(Path.Combine(cohortRoot, "Resumes"));
        Directory.CreateDirectory(Path.Combine(cohortRoot, "StudentDocuments"));
        Directory.CreateDirectory(Path.Combine(cohortRoot, "ProgressReports"));
    }

    private static string BuildCohortFolderName(Cohort cohort)
    {
        var rawName = string.IsNullOrWhiteSpace(cohort.description) ? $"Cohort_{cohort.cohort_id}" : cohort.description.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(rawName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        sanitized = string.Join("_", sanitized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

        return string.IsNullOrWhiteSpace(sanitized) ? $"Cohort_{cohort.cohort_id}" : sanitized;
    }
}
