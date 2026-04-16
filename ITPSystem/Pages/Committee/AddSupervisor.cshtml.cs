using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

public class CommitteeAddSupervisorModel : CommitteePageModelBase
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

    public CommitteeAddSupervisorModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public string NextStaffId { get; private set; } = "US001";

    public IReadOnlyList<SelectListItem> FacultyOptions { get; } = AllowedFaculties
        .Select(x => new SelectListItem(x, x))
        .ToList();

    public IReadOnlyList<SelectListItem> CampusOptions { get; } = AllowedCampuses
        .Select(x => new SelectListItem(x, x))
        .ToList();

    [BindProperty]
    public CreateSupervisorInput Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        NextStaffId = GenerateNextStaffId();
        return Page();
    }

    public IActionResult OnPostCreate()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        NextStaffId = GenerateNextStaffId();
        ModelState.Clear();
        NormalizeInput();
        TryValidateModel(Input, nameof(Input));

        if (!string.IsNullOrWhiteSpace(Input.email) &&
            _db.UcSupervisors.Any(x => x.email == Input.email))
        {
            ModelState.AddModelError("Input.email", "Email already exists.");
        }

        if (!ModelState.IsValid)
        {
            ErrorMessage = BuildValidationMessage();
            return Page();
        }

        var supervisor = new UcSupervisor
        {
            staffId = NextStaffId,
            name = Input.name,
            email = Input.email,
            contact = Input.contact,
            password = NormalizePassword(Input.icNumber),
            remark = Input.remark,
            isActive = Input.isActive,
            isCommittee = Input.isCommittee,
            faculty = Input.faculty,
            campus = Input.campus
        };

        try
        {
            _db.UcSupervisors.Add(supervisor);
            _db.SaveChanges();
        }
        catch (DbUpdateException ex)
        {
            ModelState.AddModelError("", ex.InnerException?.Message ?? ex.Message);
            ErrorMessage = BuildValidationMessage();
            return Page();
        }

        TempData["StatusMessage"] = "University supervisor added successfully.";
        return RedirectToPage("/Committee/UniSupervisors");
    }

    private void NormalizeInput()
    {
        Input.name = Input.name?.Trim() ?? string.Empty;
        Input.email = string.IsNullOrWhiteSpace(Input.email) ? null : Input.email.Trim();
        Input.contact = string.IsNullOrWhiteSpace(Input.contact) ? null : Input.contact.Trim();
        Input.icNumber = string.IsNullOrWhiteSpace(Input.icNumber) ? null : Input.icNumber.Trim();
        Input.remark = string.IsNullOrWhiteSpace(Input.remark) ? null : Input.remark.Trim();
        Input.faculty = string.IsNullOrWhiteSpace(Input.faculty) ? null : Input.faculty.Trim();
        Input.campus = string.IsNullOrWhiteSpace(Input.campus) ? null : Input.campus.Trim();

        if (string.IsNullOrWhiteSpace(Input.icNumber))
        {
            ModelState.AddModelError("Input.icNumber", "IC number is required.");
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

    private string GenerateNextStaffId()
    {
        var existingIds = _db.UcSupervisors.AsNoTracking()
            .Select(x => x.staffId)
            .ToList();

        var maxNumber = 0;
        foreach (var id in existingIds)
        {
            if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("US", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var digits = new string(id.Skip(2).Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var value))
            {
                maxNumber = Math.Max(maxNumber, value);
            }
        }

        return $"US{maxNumber + 1:D3}";
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

    private static string NormalizePassword(string? icNumber)
    {
        if (string.IsNullOrWhiteSpace(icNumber))
        {
            return string.Empty;
        }

        return new string(icNumber.Where(char.IsLetterOrDigit).ToArray());
    }

    public class CreateSupervisorInput
    {
        [Required]
        [StringLength(150)]
        public string name { get; set; } = string.Empty;

        [StringLength(250)]
        public string? email { get; set; }

        [StringLength(20)]
        public string? contact { get; set; }

        [Required]
        [StringLength(20)]
        public string? icNumber { get; set; }

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
