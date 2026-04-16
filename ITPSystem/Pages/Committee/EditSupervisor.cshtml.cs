using ITPSystem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

public class CommitteeEditSupervisorModel : CommitteePageModelBase
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

    public CommitteeEditSupervisorModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<SelectListItem> FacultyOptions { get; } = AllowedFaculties
        .Select(x => new SelectListItem(x, x))
        .ToList();

    public IReadOnlyList<SelectListItem> CampusOptions { get; } = AllowedCampuses
        .Select(x => new SelectListItem(x, x))
        .ToList();

    [BindProperty]
    public EditSupervisorInput Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet(string staffId)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        if (string.IsNullOrWhiteSpace(staffId))
        {
            TempData["StatusMessage"] = "Supervisor was not found.";
            return RedirectToPage("/Committee/UniSupervisors");
        }

        var supervisor = _db.UcSupervisors.AsNoTracking()
            .FirstOrDefault(x => x.staffId == staffId);
        if (supervisor == null)
        {
            TempData["StatusMessage"] = "Supervisor was not found.";
            return RedirectToPage("/Committee/UniSupervisors");
        }

        Input = new EditSupervisorInput
        {
            staffId = supervisor.staffId,
            name = supervisor.name,
            email = supervisor.email,
            contact = supervisor.contact,
            remark = supervisor.remark,
            isActive = supervisor.isActive,
            isCommittee = supervisor.isCommittee,
            faculty = supervisor.faculty,
            campus = supervisor.campus
        };

        return Page();
    }

    public IActionResult OnPostSave()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        ModelState.Clear();
        NormalizeInput();
        TryValidateModel(Input, nameof(Input));

        if (!ModelState.IsValid)
        {
            ErrorMessage = BuildValidationMessage();
            return Page();
        }

        var supervisor = _db.UcSupervisors.FirstOrDefault(x => x.staffId == Input.staffId);
        if (supervisor == null)
        {
            TempData["StatusMessage"] = "Supervisor was not found.";
            return RedirectToPage("/Committee/UniSupervisors");
        }

        if (!string.IsNullOrWhiteSpace(Input.email) &&
            _db.UcSupervisors.Any(x => x.staffId != Input.staffId && x.email == Input.email))
        {
            ModelState.AddModelError("Input.email", "Email already exists.");
        }

        if (!ModelState.IsValid)
        {
            ErrorMessage = BuildValidationMessage();
            return Page();
        }

        supervisor.name = Input.name;
        supervisor.email = Input.email;
        supervisor.contact = Input.contact;
        supervisor.remark = Input.remark;
        supervisor.isActive = Input.isActive;
        supervisor.isCommittee = Input.isCommittee;
        supervisor.faculty = Input.faculty;
        supervisor.campus = Input.campus;

        try
        {
            _db.SaveChanges();
        }
        catch (DbUpdateException ex)
        {
            ModelState.AddModelError("", ex.InnerException?.Message ?? ex.Message);
            ErrorMessage = BuildValidationMessage();
            return Page();
        }

        TempData["StatusMessage"] = "University supervisor updated successfully.";
        return RedirectToPage("/Committee/UniSupervisors");
    }

    private void NormalizeInput()
    {
        Input.staffId = (Input.staffId ?? string.Empty).Trim().ToUpperInvariant();
        Input.name = Input.name?.Trim() ?? string.Empty;
        Input.email = string.IsNullOrWhiteSpace(Input.email) ? null : Input.email.Trim();
        Input.contact = string.IsNullOrWhiteSpace(Input.contact) ? null : Input.contact.Trim();
        Input.remark = string.IsNullOrWhiteSpace(Input.remark) ? null : Input.remark.Trim();
        Input.faculty = string.IsNullOrWhiteSpace(Input.faculty) ? null : Input.faculty.Trim();
        Input.campus = string.IsNullOrWhiteSpace(Input.campus) ? null : Input.campus.Trim();

        if (!Input.staffId.StartsWith("US", StringComparison.Ordinal))
        {
            ModelState.AddModelError("Input.staffId", "Staff ID must start with 'US' (example: US001).");
        }

        if (!string.IsNullOrWhiteSpace(Input.faculty) &&
            !AllowedFaculties.Contains(Input.faculty, StringComparer.Ordinal))
        {
            ModelState.AddModelError("Input.faculty", "Invalid faculty value.");
        }

        if (!string.IsNullOrWhiteSpace(Input.campus) &&
            !AllowedCampuses.Contains(Input.campus, StringComparer.Ordinal))
        {
            ModelState.AddModelError("Input.campus", "Invalid campus value.");
        }

        if (!string.IsNullOrWhiteSpace(Input.email) && !IsValidEmail(Input.email))
        {
            ModelState.AddModelError("Input.email", "Email format is invalid.");
        }

        if (Input.isCommittee)
        {
            if (string.IsNullOrWhiteSpace(Input.email))
            {
                ModelState.AddModelError("Input.email", "Email is required for committee members.");
            }

            if (!Input.isActive)
            {
                ModelState.AddModelError("Input.isActive", "Committee members must be active.");
            }
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

        [StringLength(20)]
        public string? contact { get; set; }

        [StringLength(150)]
        public string? remark { get; set; }

        public bool isActive { get; set; } = true;

        public bool isCommittee { get; set; }

        [StringLength(100)]
        public string? faculty { get; set; }

        [StringLength(100)]
        public string? campus { get; set; }
    }
}
