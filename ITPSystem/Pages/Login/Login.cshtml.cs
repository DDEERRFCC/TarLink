using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ITPSystem.Data;
using ITPSystem.Models;

public class LoginModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public LoginModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    public IActionResult OnPost()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter Email/Username and Password.";
            return Page();
        }

        var loginInput = Email.Trim();

        var user = _db.SysUsers
            .FirstOrDefault(u =>
                (u.email == loginInput || u.username == loginInput) &&
                u.password == Password);

        if (user == null)
        {
            ErrorMessage = "Invalid Email/Username or Password.";
            return Page();
        }

        if (!user.is_active)
        {
            ErrorMessage = "Your account is inactive. Please contact admin.";
            return Page();
        }

        if (user.is_locked)
        {
            ErrorMessage = "Your account is locked. Please contact admin.";
            return Page();
        }

        // Set session
        HttpContext.Session.SetString("UserRole", user.role ?? "");
        HttpContext.Session.SetString("UserID", user.user_id.ToString());
        HttpContext.Session.SetString("UserName", user.username ?? user.email);

        var normalizedRole = user.role?.Trim().ToLowerInvariant() ?? string.Empty;

        if (normalizedRole == "student")
        {
            StudentApplication? student = null;
            if (user.application_id.HasValue)
            {
                student = _db.StudentApplications.FirstOrDefault(s => s.application_id == user.application_id.Value);
            }

            if (student != null)
            {
                HttpContext.Session.SetString("StudentName", student.studentName ?? "");
                HttpContext.Session.SetString("StudentID", student.studentID ?? "");
                HttpContext.Session.SetString("UserName", student.studentName ?? "");
            }
            else
            {
                // Fallback so dashboard still has a display name.
                HttpContext.Session.SetString("UserName", user.username ?? user.email);
            }

            SuccessMessage = "Login successful.";

            user.last_login_at = DateTime.Now;
            user.login_attempts = 0;
            _db.SaveChanges();

            return RedirectToPage("/Student/Dashboard");
        }
        
        if (normalizedRole == "supervisor")
        {
            SuccessMessage = "Login successful.";
            user.last_login_at = DateTime.Now;
            user.login_attempts = 0;
            _db.SaveChanges();
            return RedirectToPage("/Supervisor/Dashboard");
        }

        if (normalizedRole == "committee")
        {
            SuccessMessage = "Login successful.";
            user.last_login_at = DateTime.Now;
            user.login_attempts = 0;
            _db.SaveChanges();
            return RedirectToPage("/Committee/Dashboard");
        }

        ErrorMessage = "Unauthorized role. Please use the correct portal.";
        return Page();
    }
}
