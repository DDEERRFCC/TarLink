using ITPSystem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

public class StudentChangePasswordModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public StudentChangePasswordModel(ApplicationDbContext db)
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
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [StringLength(255, MinimumLength = 6, ErrorMessage = "New password must be at least 6 characters.")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Confirm password does not match the new password.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        if (!IsStudentLoggedIn())
        {
            return RedirectToPage("/Login/StudentLogin");
        }

        return Page();
    }

    public IActionResult OnPost()
    {
        if (!IsStudentLoggedIn())
        {
            return RedirectToPage("/Login/StudentLogin");
        }

        var userId = GetUserIdFromSession();
        if (userId == null)
        {
            return RedirectToPage("/Login/StudentLogin");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = _db.SysUsers.FirstOrDefault(u => u.user_id == userId.Value);
        if (user == null)
        {
            ModelState.AddModelError("", "User account was not found.");
            return Page();
        }

        if (!string.Equals(user.password ?? string.Empty, Input.CurrentPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError("Input.CurrentPassword", "Current password is incorrect.");
            return Page();
        }

        user.password = Input.NewPassword;
        _db.SaveChanges();
        StatusMessage = "Password updated successfully.";
        return RedirectToPage();
    }

    private bool IsStudentLoggedIn()
    {
        var role = HttpContext.Session.GetString("UserRole");
        return string.Equals(role, "student", StringComparison.OrdinalIgnoreCase);
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
