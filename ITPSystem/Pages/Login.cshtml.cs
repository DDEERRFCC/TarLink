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

    public IActionResult OnPost()
    {
        if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password))
        {
            ErrorMessage = "Please enter Email and Password.";
            return Page();
        }

        var user = _db.SysUsers
            .FirstOrDefault(u => u.email == Email && u.password_hash == Password);

        if (user == null)
        {
            ErrorMessage = "Invalid Email or Password.";
            return Page();
        }

        // Set session
        HttpContext.Session.SetString("UserRole", user.role ?? "");
        HttpContext.Session.SetString("UserID", user.user_id.ToString());

        if (user.role == "Student" && user.application_id.HasValue)
        {
            var student = _db.StudentApplications.FirstOrDefault(s => s.application_id == user.application_id.Value);
            if (student != null)
            {
                HttpContext.Session.SetString("StudentName", student.studentName ?? "");
                HttpContext.Session.SetString("StudentID", student.studentID ?? "");
            }

            return RedirectToPage("/Student/Dashboard");
        }

        // Add Supervisor / Committee later
        return Page();
    }
}
