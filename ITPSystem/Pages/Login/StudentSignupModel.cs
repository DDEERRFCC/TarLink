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

    // For Cohort dropdown
    public List<Cohort> Cohorts { get; set; } = new List<Cohort>();

    [BindProperty]
    public StudentApplication Student { get; set; } = new();

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string ICNumber { get; set; } = string.Empty;

    [TempData]
    public string? Message { get; set; }

    //Shows available cohorts on signup page

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
        LoadCohorts(); // ✅ Must reload for postbacks

        if (!ModelState.IsValid)
            return Page();

        using var tx = _db.Database.BeginTransaction();

        try
        {
            _db.StudentApplications.Add(Student);
            _db.SaveChanges();

            var sysuser = new SysUser
            {
                email = Email,
                username = Username,
                ic_number = ICNumber,
                password = ICNumber,
                role = "student",
                application_id = Student.application_id,
                is_active = true,
                is_locked = false
            };
            _db.SysUsers.Add(sysuser);
            _db.SaveChanges();

            tx.Commit();

            Message = "Signup successful! Please login using your email and NRIC.";
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
