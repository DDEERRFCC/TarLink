using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

public class StudentProfileModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public StudentProfileModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public StudentApplication? Student { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required]
        [StringLength(255)]
        public string studentName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string studentEmail { get; set; } = string.Empty;

        [EmailAddress]
        [StringLength(255)]
        public string? personalEmail { get; set; }

        [StringLength(500)]
        public string? tempAddress { get; set; }

        public string? permanentAddress { get; set; }

        [StringLength(45)]
        public string? permanentContact { get; set; }

        public bool ownTransport { get; set; }

        public string? healthRemark { get; set; }

        [StringLength(255)]
        public string? Knowledge { get; set; }

        [Range(0, 4.00, ErrorMessage = "CGPA must be between 0.00 and 4.00.")]
        public decimal? CGPA { get; set; }
    }

    public IActionResult OnGet()
    {
        var role = HttpContext.Session.GetString("UserRole");
        if (!string.Equals(role, "student", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToPage("/Login/StudentLogin");
        }

        var userId = GetUserIdFromSession();
        if (userId == null)
        {
            return RedirectToPage("/Login/StudentLogin");
        }

        LoadStudentProfile(userId.Value);
        if (Student == null)
        {
            StatusMessage = "No linked student application found for this account.";
            return Page();
        }

        return Page();
    }

    public IActionResult OnPost()
    {
        var role = HttpContext.Session.GetString("UserRole");
        if (!string.Equals(role, "student", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToPage("/Login/StudentLogin");
        }

        var userId = GetUserIdFromSession();
        if (userId == null)
        {
            return RedirectToPage("/Login/StudentLogin");
        }

        var user = _db.SysUsers.FirstOrDefault(u => u.user_id == userId.Value);
        if (user?.application_id == null)
        {
            ModelState.AddModelError("", "No linked student application found.");
            LoadStudentProfile(userId.Value);
            return Page();
        }

        Student = _db.StudentApplications.FirstOrDefault(s => s.application_id == user.application_id.Value);
        if (Student == null)
        {
            ModelState.AddModelError("", "Student profile was not found.");
            return Page();
        }

        if (!ModelState.IsValid)
        {
            LoadProfileInputFromStudent();
            return Page();
        }

        Student.studentName = Input.studentName.Trim();
        Student.studentEmail = Input.studentEmail.Trim();
        Student.personalEmail = string.IsNullOrWhiteSpace(Input.personalEmail) ? null : Input.personalEmail.Trim();
        Student.tempAddress = string.IsNullOrWhiteSpace(Input.tempAddress) ? null : Input.tempAddress.Trim();
        Student.permanentAddress = string.IsNullOrWhiteSpace(Input.permanentAddress) ? null : Input.permanentAddress.Trim();
        Student.permanentContact = string.IsNullOrWhiteSpace(Input.permanentContact) ? null : Input.permanentContact.Trim();
        Student.ownTransport = Input.ownTransport;
        Student.healthRemark = string.IsNullOrWhiteSpace(Input.healthRemark) ? null : Input.healthRemark.Trim();
        Student.Knowledge = string.IsNullOrWhiteSpace(Input.Knowledge) ? null : Input.Knowledge.Trim();
        Student.CGPA = Input.CGPA;
        Student.updated_at = DateTime.Now;

        try
        {
            _db.SaveChanges();
            HttpContext.Session.SetString("UserName", Student.studentName ?? user.username ?? user.email);
            StatusMessage = "Profile updated successfully.";
            return RedirectToPage();
        }
        catch (DbUpdateException ex)
        {
            var root = ex.InnerException?.Message ?? ex.Message;
            ModelState.AddModelError("", "Update failed: " + root);
            return Page();
        }
    }

    public IActionResult OnPostLogout()
    {
        HttpContext.Session.Clear();
        return RedirectToPage("/Login/StudentLogin");
    }

    private void LoadStudentProfile(int userId)
    {
        var user = _db.SysUsers.FirstOrDefault(u => u.user_id == userId);
        if (user?.application_id == null)
        {
            return;
        }

        Student = _db.StudentApplications.FirstOrDefault(s => s.application_id == user.application_id.Value);
        LoadProfileInputFromStudent();
    }

    private void LoadProfileInputFromStudent()
    {
        if (Student == null)
        {
            return;
        }

        Input = new InputModel
        {
            studentName = Student.studentName,
            studentEmail = Student.studentEmail,
            personalEmail = Student.personalEmail,
            tempAddress = Student.tempAddress,
            permanentAddress = Student.permanentAddress,
            permanentContact = Student.permanentContact,
            ownTransport = Student.ownTransport,
            healthRemark = Student.healthRemark,
            Knowledge = Student.Knowledge,
            CGPA = Student.CGPA
        };
    }

    private int? GetUserIdFromSession()
    {
        var userIdString = HttpContext.Session.GetString("UserID");
        if (int.TryParse(userIdString, out var userId))
        {
            return userId;
        }

        return null;
    }
}

