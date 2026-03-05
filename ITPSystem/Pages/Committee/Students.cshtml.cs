using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class CommitteeStudentsModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeStudentsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<StudentApplication> Students { get; private set; } = new();

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        Students = _db.StudentApplications.AsNoTracking()
            .Include(s => s.Cohort)
            .OrderBy(s => s.studentName)
            .ToList();

        return Page();
    }
}
