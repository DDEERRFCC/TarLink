using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ITPSystem.Data;
using ITPSystem.Models;

public class StudentSignupModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public StudentSignupModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public StudentApplication Student { get; set; } = new StudentApplication();

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string ICNumber { get; set; } = string.Empty;

    [TempData]
    public string? Message { get; set; }

    public void OnGet()
    {
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // 1️⃣ Insert into studentapplication first
        Student.timeStamp = DateTime.Now;
        _db.StudentApplications.Add(Student);
        _db.SaveChanges();

        // 2️⃣ Insert into sysuser for login
        var sysuser = new SysUser
        {
            email = Email,
            username = Username,
            ic_number = ICNumber,
            password = ICNumber, // default password = IC/NRIC
            role = "Student",
            application_id = Student.application_id,
        };

        _db.SysUsers.Add(sysuser);
        _db.SaveChanges();

        Message = "Signup successful! Please login using your email and NRIC.";
        return RedirectToPage("/Login");
    }
}
