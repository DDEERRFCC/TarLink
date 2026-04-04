using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

public class ResetPasswordModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public ResetPasswordModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public string Token { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword))]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public bool IsTokenValid { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? StatusMessage { get; private set; }

    public void OnGet()
    {
        IsTokenValid = FindUserByToken(Token) != null;
    }

    public IActionResult OnPost()
    {
        var user = FindUserByToken(Token);
        IsTokenValid = user != null;

        if (!IsTokenValid)
        {
            ErrorMessage = "This reset link is invalid or has expired.";
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        user!.password = NewPassword;
        user.password_reset_token = null;
        user.password_reset_expires = null;
        user.password_changed_at = DateTime.Now;
        user.updated_at = DateTime.Now;
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Password updated successfully. Please log in again.";

        return RedirectToPage("/Login/StudentLogin");
    }

    private SysUser? FindUserByToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        return _db.SysUsers.FirstOrDefault(u =>
            u.password_reset_token == token &&
            u.password_reset_expires.HasValue &&
            u.password_reset_expires.Value >= now);
    }
}
