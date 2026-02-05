using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

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

        // Hash password using SHA256
        string hashedPassword = HashPassword(Password);

        var user = _db.Users
            .FirstOrDefault(u => u.Email == Email && u.PasswordHash == hashedPassword);

        if (user == null)
        {
            ErrorMessage = "Invalid Email or Password.";
            return Page();
        }

        // Set session values
        HttpContext.Session.SetString("UserRole", user.Role!);
        HttpContext.Session.SetString("UserName", user.Name!);

        // Redirect based on role
        return user.Role switch
        {
            "Student" => RedirectToPage("/Student/Dashboard"),
            "Committee" => RedirectToPage("/Committee/Dashboard"),
            "Supervisor" => RedirectToPage("/Supervisor/Dashboard"),
            _ => Page()
        };
    }

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }
}
