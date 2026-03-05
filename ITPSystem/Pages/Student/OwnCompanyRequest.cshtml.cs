using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

public class OwnCompanyRequestModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public OwnCompanyRequestModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string StudentEmail { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string CompanyAddress { get; set; } = string.Empty;

        public bool ConfirmNoRelationship { get; set; }
    }

    public IActionResult OnGet()
    {
        if (!IsStudent())
        {
            return RedirectToPage("/Login/StudentLogin");
        }

        var student = GetCurrentStudentApplication(asNoTracking: true);
        if (student != null)
        {
            Input.StudentEmail = student.studentEmail ?? string.Empty;
            Input.CompanyName = student.comName ?? string.Empty;
            Input.CompanyAddress = student.comAddress ?? string.Empty;
            Input.ConfirmNoRelationship = student.isAgreed;
        }
        else
        {
            Input.StudentEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
        }

        return Page();
    }

    public IActionResult OnPost()
    {
        if (!IsStudent())
        {
            return RedirectToPage("/Login/StudentLogin");
        }

        var student = GetCurrentStudentApplication(asNoTracking: false);
        if (student == null)
        {
            ModelState.AddModelError("", "No linked student application found for this account.");
            return Page();
        }

        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return RedirectToPage("/Login/StudentLogin");
        }

        if (!Input.ConfirmNoRelationship)
        {
            ModelState.AddModelError(nameof(Input.ConfirmNoRelationship), "You must confirm this declaration.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        student.comName = Input.CompanyName.Trim();
        student.comAddress = Input.CompanyAddress.Trim();
        student.isAgreed = Input.ConfirmNoRelationship;
        student.updated_at = DateTime.Now;

        var pendingRequest = _db.CompanyRequests
            .Where(r => r.requested_by == userId.Value && r.status == "pending")
            .OrderByDescending(r => r.requested_at)
            .FirstOrDefault();

        if (pendingRequest == null)
        {
            _db.CompanyRequests.Add(new CompanyRequest
            {
                requested_by = userId.Value,
                company_name = student.comName ?? Input.CompanyName.Trim(),
                address1 = student.comAddress ?? Input.CompanyAddress.Trim(),
                contact_name = student.studentName,
                contact_email = Input.StudentEmail.Trim(),
                contact_phone = student.permanentContact,
                status = "pending",
                decision_remark = null,
                reviewed_by = null,
                reviewed_at = null,
                requested_at = DateTime.Now,
                updated_at = DateTime.Now
            });
        }
        else
        {
            pendingRequest.company_name = student.comName ?? Input.CompanyName.Trim();
            pendingRequest.address1 = student.comAddress ?? Input.CompanyAddress.Trim();
            pendingRequest.contact_name = student.studentName;
            pendingRequest.contact_email = Input.StudentEmail.Trim();
            pendingRequest.contact_phone = student.permanentContact;
            pendingRequest.updated_at = DateTime.Now;
        }

        _db.SaveChanges();
        StatusMessage = "Own company request details saved successfully.";
        return RedirectToPage();
    }

    private bool IsStudent()
    {
        var role = HttpContext.Session.GetString("UserRole");
        return string.Equals(role, "student", StringComparison.OrdinalIgnoreCase);
    }

    private StudentApplication? GetCurrentStudentApplication(bool asNoTracking)
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

        var studentQuery = asNoTracking ? _db.StudentApplications.AsNoTracking() : _db.StudentApplications;
        return studentQuery.FirstOrDefault(s => s.application_id == user.application_id.Value);
    }

    private int? GetCurrentUserId()
    {
        var userIdText = HttpContext.Session.GetString("UserID");
        return int.TryParse(userIdText, out var userId) ? userId : null;
    }
}

