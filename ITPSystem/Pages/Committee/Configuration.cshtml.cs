using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

public class CommitteeConfigurationModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeConfigurationModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<Cohort> Cohorts { get; private set; } = new();
    public List<StudentAccessItem> StudentAccesses { get; private set; } = new();

    public bool ShowEditForm { get; private set; }

    [BindProperty]
    public CreateCohortInput CreateInput { get; set; } = new();

    [BindProperty]
    public EditCohortInput EditInput { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public IActionResult OnGet(int? editId = null)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        LoadData();
        if (editId.HasValue)
        {
            LoadEditModel(editId.Value);
        }

        return Page();
    }

    public IActionResult OnPostCreate()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        ModelState.Clear();
        NormalizeCreateInput();
        TryValidateModel(CreateInput, nameof(CreateInput));

        if (!ModelState.IsValid)
        {
            LoadData();
            return Page();
        }

        _db.Cohorts.Add(new Cohort
        {
            description = CreateInput.description,
            startDate = CreateInput.startDate,
            endDate = CreateInput.endDate,
            level = CreateInput.level,
            isActive = CreateInput.isActive,
            finalReportDueDate = CreateInput.finalReportDueDate,
            campus = CreateInput.campus,
            faculty = CreateInput.faculty,
            personInCharge = CreateInput.personInCharge,
            pidEmail = CreateInput.pidEmail
        });

        _db.SaveChanges();
        StatusMessage = "Cohort created successfully.";
        return RedirectToPage();
    }

    public IActionResult OnPostUpdate()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        ModelState.Clear();
        NormalizeEditInput();
        TryValidateModel(EditInput, nameof(EditInput));

        if (!ModelState.IsValid)
        {
            LoadData();
            ShowEditForm = true;
            return Page();
        }

        var cohort = _db.Cohorts.FirstOrDefault(c => c.cohort_id == EditInput.cohort_id);
        if (cohort == null)
        {
            LoadData();
            ShowEditForm = true;
            StatusMessage = "Cohort not found.";
            return Page();
        }

        cohort.description = EditInput.description;
        cohort.startDate = EditInput.startDate;
        cohort.endDate = EditInput.endDate;
        cohort.level = EditInput.level;
        cohort.isActive = EditInput.isActive;
        cohort.finalReportDueDate = EditInput.finalReportDueDate;
        cohort.campus = EditInput.campus;
        cohort.faculty = EditInput.faculty;
        cohort.personInCharge = EditInput.personInCharge;
        cohort.pidEmail = EditInput.pidEmail;

        _db.SaveChanges();
        StatusMessage = "Cohort updated successfully.";
        return RedirectToPage();
    }

    public IActionResult OnPostDelete(int cohortId)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var cohort = _db.Cohorts.FirstOrDefault(c => c.cohort_id == cohortId);
        if (cohort == null)
        {
            StatusMessage = "Cohort not found.";
            return RedirectToPage();
        }

        if (_db.StudentApplications.Any(s => s.cohortId == cohortId))
        {
            StatusMessage = "Cannot delete cohort because students are linked to it.";
            return RedirectToPage();
        }

        _db.Cohorts.Remove(cohort);
        _db.SaveChanges();
        StatusMessage = "Cohort deleted successfully.";
        return RedirectToPage();
    }

    private void LoadData()
    {
        Cohorts = _db.Cohorts.AsNoTracking()
            .OrderByDescending(c => c.startDate)
            .ToList();

        var students = _db.StudentApplications.AsNoTracking()
            .Include(s => s.Cohort)
            .ToDictionary(s => s.application_id);

        StudentAccesses = _db.SysUsers.AsNoTracking()
            .Where(u => u.role == "student")
            .OrderBy(u => u.username)
            .ToList()
            .Select(u =>
            {
                students.TryGetValue(u.application_id ?? -1, out var app);
                return new StudentAccessItem
                {
                    UserId = u.user_id,
                    StudentId = app?.studentID ?? u.username,
                    StudentName = app?.studentName ?? "-",
                    AccessStatus = u.is_active ? "Active" : "Inactive",
                    Cohort = app?.Cohort == null
                        ? "-"
                        : $"{app.Cohort.cohort_id} - {app.Cohort.description}"
                };
            })
            .ToList();
    }

    private void LoadEditModel(int cohortId)
    {
        var cohort = _db.Cohorts.AsNoTracking().FirstOrDefault(c => c.cohort_id == cohortId);
        if (cohort == null)
        {
            return;
        }

        EditInput = new EditCohortInput
        {
            cohort_id = cohort.cohort_id,
            description = cohort.description,
            startDate = cohort.startDate,
            endDate = cohort.endDate,
            level = cohort.level,
            isActive = cohort.isActive,
            finalReportDueDate = cohort.finalReportDueDate,
            campus = cohort.campus,
            faculty = cohort.faculty,
            personInCharge = cohort.personInCharge,
            pidEmail = cohort.pidEmail
        };

        ShowEditForm = true;
    }

    private void NormalizeCreateInput()
    {
        CreateInput.description = Clean(CreateInput.description);
        CreateInput.campus = Clean(CreateInput.campus);
        CreateInput.faculty = Clean(CreateInput.faculty);
        CreateInput.personInCharge = Clean(CreateInput.personInCharge);
        CreateInput.pidEmail = Clean(CreateInput.pidEmail);
    }

    private void NormalizeEditInput()
    {
        EditInput.description = Clean(EditInput.description);
        EditInput.campus = Clean(EditInput.campus);
        EditInput.faculty = Clean(EditInput.faculty);
        EditInput.personInCharge = Clean(EditInput.personInCharge);
        EditInput.pidEmail = Clean(EditInput.pidEmail);
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public class StudentAccessItem
    {
        public int UserId { get; set; }
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string AccessStatus { get; set; } = string.Empty;
        public string Cohort { get; set; } = "-";
    }

    public class CreateCohortInput
    {
        [StringLength(100)]
        public string? description { get; set; }
        public DateTime? startDate { get; set; }
        public DateTime? endDate { get; set; }
        public byte? level { get; set; }
        public bool isActive { get; set; }
        public DateTime? finalReportDueDate { get; set; }
        [StringLength(45)]
        public string? campus { get; set; }
        [StringLength(45)]
        public string? faculty { get; set; }
        [StringLength(250)]
        public string? personInCharge { get; set; }
        [EmailAddress]
        [StringLength(250)]
        public string? pidEmail { get; set; }
    }

    public class EditCohortInput
    {
        [Required]
        public int cohort_id { get; set; }
        [StringLength(100)]
        public string? description { get; set; }
        public DateTime? startDate { get; set; }
        public DateTime? endDate { get; set; }
        public byte? level { get; set; }
        public bool isActive { get; set; }
        public DateTime? finalReportDueDate { get; set; }
        [StringLength(45)]
        public string? campus { get; set; }
        [StringLength(45)]
        public string? faculty { get; set; }
        [StringLength(250)]
        public string? personInCharge { get; set; }
        [EmailAddress]
        [StringLength(250)]
        public string? pidEmail { get; set; }
    }
}
