using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ITPSystem.Data;

public class CommitteeLoginModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public CommitteeLoginModel(ApplicationDbContext db)
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
        var user = _db.SysUsers.FirstOrDefault(u =>
            (u.email == loginInput || u.username == loginInput) &&
            u.password == Password);

        if (user == null)
        {
            ErrorMessage = "Invalid Email/Username or Password.";
            return Page();
        }

        if (!string.Equals(user.role, "committee", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "This account is not a committee account. Please use the correct login page.";
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

        HttpContext.Session.SetString("UserRole", user.role ?? "");
        HttpContext.Session.SetString("UserID", user.user_id.ToString());
        HttpContext.Session.SetString("UserName", user.username ?? user.email ?? "");
        HttpContext.Session.SetString("UserEmail", user.email ?? "");

        SuccessMessage = "Login successful.";
        user.last_login_at = DateTime.Now;
        user.login_attempts = 0;
        _db.SaveChanges();

        return RedirectToPage("/Committee/Dashboard");
    }
}
