using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ITPSystem.Data;

public class SupervisorLoginModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public SupervisorLoginModel(ApplicationDbContext db)
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
        var supervisor = _db.UcSupervisors.FirstOrDefault(u =>
            (u.email == loginInput || u.staffId == loginInput) &&
            u.password == Password);

        if (supervisor == null)
        {
            ErrorMessage = "Invalid Email/Username or Password.";
            return Page();
        }

        if (!supervisor.isActive)
        {
            ErrorMessage = "Your account is inactive. Please contact admin.";
            return Page();
        }

        HttpContext.Session.SetString("UserRole", "supervisor");
        HttpContext.Session.SetString("UserID", "0");
        HttpContext.Session.SetString("UserName", supervisor.name);
        HttpContext.Session.SetString("UserEmail", supervisor.email ?? "");
        HttpContext.Session.SetString("SupervisorStaffId", supervisor.staffId);

        SuccessMessage = "Login successful.";

        return RedirectToPage("/Supervisor/Dashboard");
    }
}
