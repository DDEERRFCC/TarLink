using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

public class CommitteeUniSupervisorsModel : CommitteePageModelBase
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

    public CommitteeUniSupervisorsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<SupervisorItem> Supervisors { get; private set; } = new();
    public bool ShowEditForm { get; private set; }
    public IReadOnlyList<SelectListItem> FacultyOptions { get; } = AllowedFaculties
        .Select(x => new SelectListItem(x, x))
        .ToList();
    public IReadOnlyList<SelectListItem> CampusOptions { get; } = AllowedCampuses
        .Select(x => new SelectListItem(x, x))
        .ToList();

    [BindProperty]
    public CreateSupervisorInput CreateInput { get; set; } = new();

    [BindProperty]
    public EditSupervisorInput EditInput { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public IActionResult OnGet(string? editId = null)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        LoadData();
        if (!string.IsNullOrWhiteSpace(editId))
        {
            LoadEditModel(editId);
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
            StatusMessage = BuildValidationMessage();
            LoadData();
            return Page();
        }

        if (_db.UcSupervisors.Any(x => x.staffId == CreateInput.staffId))
        {
            ModelState.AddModelError("CreateInput.staffId", "Staff ID already exists.");
        }

        if (!string.IsNullOrWhiteSpace(CreateInput.email) &&
            _db.UcSupervisors.Any(x => x.email == CreateInput.email))
        {
            ModelState.AddModelError("CreateInput.email", "Email already exists.");
        }

        if (!ModelState.IsValid)
        {
            StatusMessage = BuildValidationMessage();
            LoadData();
            return Page();
        }

        var supervisor = new UcSupervisor
        {
            staffId = CreateInput.staffId,
            name = CreateInput.name,
            email = CreateInput.email,
            password = CreateInput.password,
            remark = CreateInput.remark,
            isActive = CreateInput.isActive,
            isCommittee = CreateInput.isCommittee,
            faculty = CreateInput.faculty,
            campus = CreateInput.campus
        };

        try
        {
            _db.UcSupervisors.Add(supervisor);
            _db.SaveChanges();
        }
        catch (DbUpdateException ex)
        {
            ModelState.AddModelError("", ex.InnerException?.Message ?? ex.Message);
            StatusMessage = BuildValidationMessage();
            LoadData();
            return Page();
        }

        StatusMessage = "University supervisor added successfully.";
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
            StatusMessage = BuildValidationMessage();
            LoadData();
            ShowEditForm = true;
            return Page();
        }

        var existing = _db.UcSupervisors.FirstOrDefault(x => x.staffId == EditInput.staffId);
        if (existing == null)
        {
            LoadData();
            ModelState.AddModelError("", "Supervisor not found.");
            ShowEditForm = true;
            return Page();
        }

        if (!string.IsNullOrWhiteSpace(EditInput.email) &&
            _db.UcSupervisors.Any(x => x.staffId != EditInput.staffId && x.email == EditInput.email))
        {
            ModelState.AddModelError("EditInput.email", "Email already exists.");
        }

        if (!ModelState.IsValid)
        {
            StatusMessage = BuildValidationMessage();
            LoadData();
            ShowEditForm = true;
            return Page();
        }

        existing.name = EditInput.name;
        existing.email = EditInput.email;
        existing.password = EditInput.password;
        existing.remark = EditInput.remark;
        existing.isActive = EditInput.isActive;
        existing.isCommittee = EditInput.isCommittee;
        existing.faculty = EditInput.faculty;
        existing.campus = EditInput.campus;

        try
        {
            _db.SaveChanges();
        }
        catch (DbUpdateException ex)
        {
            ModelState.AddModelError("", ex.InnerException?.Message ?? ex.Message);
            StatusMessage = BuildValidationMessage();
            LoadData();
            ShowEditForm = true;
            return Page();
        }

        StatusMessage = "University supervisor updated successfully.";
        return RedirectToPage();
    }

    public IActionResult OnPostDelete(string staffId)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var existing = _db.UcSupervisors.FirstOrDefault(x => x.staffId == staffId);
        if (existing == null)
        {
            StatusMessage = "Supervisor record was not found.";
            return RedirectToPage();
        }

        _db.UcSupervisors.Remove(existing);
        _db.SaveChanges();

        StatusMessage = "University supervisor deleted successfully.";
        return RedirectToPage();
    }

    private void LoadData()
    {
        Supervisors = _db.UcSupervisors.AsNoTracking()
            .OrderBy(x => x.name)
            .Select(x => new SupervisorItem
            {
                staffId = x.staffId,
                name = x.name,
                email = x.email,
                remark = x.remark,
                faculty = x.faculty,
                campus = x.campus,
                isActive = x.isActive,
                isCommittee = x.isCommittee,
                created_at = x.created_at,
                StudentCount = _db.StudentApplications.Count(s =>
                    (!string.IsNullOrWhiteSpace(x.email) && s.ucSupervisorEmail == x.email)
                    || (string.IsNullOrWhiteSpace(s.ucSupervisorEmail) && s.ucSupervisor == x.name))
            })
            .ToList();
    }

    private void LoadEditModel(string staffId)
    {
        var supervisor = _db.UcSupervisors.AsNoTracking()
            .FirstOrDefault(x => x.staffId == staffId);
        if (supervisor == null)
        {
            return;
        }

        EditInput = new EditSupervisorInput
        {
            staffId = supervisor.staffId,
            name = supervisor.name,
            email = supervisor.email,
            password = supervisor.password,
            remark = supervisor.remark,
            isActive = supervisor.isActive,
            isCommittee = supervisor.isCommittee,
            faculty = supervisor.faculty,
            campus = supervisor.campus
        };

        ShowEditForm = true;
    }

    private void NormalizeCreateInput()
    {
        CreateInput.staffId = (CreateInput.staffId ?? string.Empty).Trim().ToUpperInvariant();
        CreateInput.name = CreateInput.name?.Trim() ?? string.Empty;
        CreateInput.email = string.IsNullOrWhiteSpace(CreateInput.email) ? null : CreateInput.email.Trim();
        CreateInput.password = string.IsNullOrWhiteSpace(CreateInput.password) ? null : CreateInput.password.Trim();
        CreateInput.remark = string.IsNullOrWhiteSpace(CreateInput.remark) ? null : CreateInput.remark.Trim();
        CreateInput.faculty = string.IsNullOrWhiteSpace(CreateInput.faculty) ? null : CreateInput.faculty.Trim();
        CreateInput.campus = string.IsNullOrWhiteSpace(CreateInput.campus) ? null : CreateInput.campus.Trim();

        if (!CreateInput.staffId.StartsWith("US", StringComparison.Ordinal))
        {
            ModelState.AddModelError("CreateInput.staffId", "Staff ID must start with 'US' (example: US001).");
        }

        if (!string.IsNullOrWhiteSpace(CreateInput.faculty) &&
            !AllowedFaculties.Contains(CreateInput.faculty, StringComparer.Ordinal))
        {
            ModelState.AddModelError("CreateInput.faculty", "Invalid faculty value.");
        }

        if (!string.IsNullOrWhiteSpace(CreateInput.campus) &&
            !AllowedCampuses.Contains(CreateInput.campus, StringComparer.Ordinal))
        {
            ModelState.AddModelError("CreateInput.campus", "Invalid campus value.");
        }

        if (!string.IsNullOrWhiteSpace(CreateInput.email) && !IsValidEmail(CreateInput.email))
        {
            ModelState.AddModelError("CreateInput.email", "Email format is invalid.");
        }
    }

    private void NormalizeEditInput()
    {
        EditInput.staffId = (EditInput.staffId ?? string.Empty).Trim().ToUpperInvariant();
        EditInput.name = EditInput.name?.Trim() ?? string.Empty;
        EditInput.email = string.IsNullOrWhiteSpace(EditInput.email) ? null : EditInput.email.Trim();
        EditInput.password = string.IsNullOrWhiteSpace(EditInput.password) ? null : EditInput.password.Trim();
        EditInput.remark = string.IsNullOrWhiteSpace(EditInput.remark) ? null : EditInput.remark.Trim();
        EditInput.faculty = string.IsNullOrWhiteSpace(EditInput.faculty) ? null : EditInput.faculty.Trim();
        EditInput.campus = string.IsNullOrWhiteSpace(EditInput.campus) ? null : EditInput.campus.Trim();

        if (!EditInput.staffId.StartsWith("US", StringComparison.Ordinal))
        {
            ModelState.AddModelError("EditInput.staffId", "Staff ID must start with 'US' (example: US001).");
        }

        if (!string.IsNullOrWhiteSpace(EditInput.faculty) &&
            !AllowedFaculties.Contains(EditInput.faculty, StringComparer.Ordinal))
        {
            ModelState.AddModelError("EditInput.faculty", "Invalid faculty value.");
        }

        if (!string.IsNullOrWhiteSpace(EditInput.campus) &&
            !AllowedCampuses.Contains(EditInput.campus, StringComparer.Ordinal))
        {
            ModelState.AddModelError("EditInput.campus", "Invalid campus value.");
        }

        if (!string.IsNullOrWhiteSpace(EditInput.email) && !IsValidEmail(EditInput.email))
        {
            ModelState.AddModelError("EditInput.email", "Email format is invalid.");
        }
    }

    private string BuildValidationMessage()
    {
        var first = ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(msg => !string.IsNullOrWhiteSpace(msg));

        return string.IsNullOrWhiteSpace(first) ? "Validation failed." : first;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public class SupervisorItem
    {
        public string staffId { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string? email { get; set; }
        public string? remark { get; set; }
        public string? faculty { get; set; }
        public string? campus { get; set; }
        public bool isActive { get; set; }
        public bool isCommittee { get; set; }
        public DateTime created_at { get; set; }
        public int StudentCount { get; set; }
    }

    public class CreateSupervisorInput
    {
        [Required]
        [StringLength(16)]
        [RegularExpression(@"^US[A-Za-z0-9]{1,14}$", ErrorMessage = "Staff ID must start with 'US' and contain only letters/numbers (example: US001).")]
        public string staffId { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string name { get; set; } = string.Empty;

        [StringLength(250)]
        public string? email { get; set; }

        [StringLength(255)]
        public string? password { get; set; }

        [StringLength(150)]
        public string? remark { get; set; }

        public bool isActive { get; set; } = true;

        public bool isCommittee { get; set; } = false;

        [StringLength(75)]
        public string? faculty { get; set; }

        [StringLength(45)]
        public string? campus { get; set; }
    }

    public class EditSupervisorInput
    {
        [Required]
        [StringLength(16)]
        [RegularExpression(@"^US[A-Za-z0-9]{1,14}$", ErrorMessage = "Staff ID must start with 'US' and contain only letters/numbers (example: US001).")]
        public string staffId { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string name { get; set; } = string.Empty;

        [StringLength(250)]
        public string? email { get; set; }

        [StringLength(255)]
        public string? password { get; set; }

        [StringLength(150)]
        public string? remark { get; set; }

        public bool isActive { get; set; } = true;

        public bool isCommittee { get; set; } = false;

        [StringLength(75)]
        public string? faculty { get; set; }

        [StringLength(45)]
        public string? campus { get; set; }
    }
}
