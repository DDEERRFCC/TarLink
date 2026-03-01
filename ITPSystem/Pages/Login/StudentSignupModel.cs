using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ITPSystem.Data;
using ITPSystem.Models;
using System.ComponentModel.DataAnnotations;

public class StudentSignupModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public StudentSignupModel(ApplicationDbContext db)
    {
        _db = db;
    }

    // For Cohort dropdown
    public List<Cohort> Cohorts { get; set; } = new List<Cohort>();

    [BindProperty]
    public StudentApplication Student { get; set; } = new();

    [BindProperty]
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    public string ICNumber { get; set; } = string.Empty;

    [TempData]
    public string? Message { get; set; }

    // Shows available cohorts on signup page
    public void LoadCohorts()
    {
        var today = DateTime.Today;
        Cohorts = _db.Cohorts
            .Where(c => c.isActive
                        && c.startDate <= today
                        && c.endDate >= today)
            .OrderBy(c => c.startDate)
            .ToList();
    }

    public void OnGet()
    {
        LoadCohorts();
    }

    public IActionResult OnPost()
    {
        LoadCohorts(); // Must reload for postbacks
        Email = Email?.Trim() ?? string.Empty;
        Username = Username?.Trim() ?? string.Empty;
        ICNumber = ICNumber?.Trim() ?? string.Empty;

        if (!ModelState.IsValid)
            return Page();

        using var tx = _db.Database.BeginTransaction();

        try
        {
            var now = DateTime.Now;

            // Ensure DB-required timestamp and default columns are valid before insert.
            Student.created_at = now;
            Student.updated_at = now;
            Student.gender = string.IsNullOrWhiteSpace(Student.gender) ? "O" : Student.gender.Trim().ToUpperInvariant();
            Student.applyStatus = string.IsNullOrWhiteSpace(Student.applyStatus) ? "pending" : Student.applyStatus.Trim().ToLowerInvariant();

            _db.StudentApplications.Add(Student);
            _db.SaveChanges();

            var sysuser = new SysUser
            {
                email = Email,
                username = Username,
                ic_number = ICNumber,
                password = ICNumber.Replace("-", ""),
                role = "student",
                application_id = Student.application_id,
                is_active = true,
                is_locked = false,
                created_at = now,
                updated_at = now
            };
            _db.SysUsers.Add(sysuser);
            _db.SaveChanges();

            tx.Commit();

            Message = "Signup successful! Please login using your email and NRIC.";
            return Page();
        }
        catch (DbUpdateException ex)
        {
            tx.Rollback();
            var root = ex.InnerException?.Message ?? ex.Message;
            ModelState.AddModelError("", "Signup failed: " + root);
            return Page();
        }
        catch (Exception ex)
        {
            tx.Rollback();
            ModelState.AddModelError("", "Signup failed: " + ex.Message);
            return Page();
        }
    }
}
