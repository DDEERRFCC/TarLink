using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class CommitteeUnregisteredStudentsModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeUnregisteredStudentsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<StudentApplication> UnregisteredStudents { get; private set; } = new();

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var registeredAppIds = _db.SysUsers.AsNoTracking()
            .Where(u => u.application_id != null)
            .Select(u => u.application_id!.Value)
            .ToHashSet();

        UnregisteredStudents = _db.StudentApplications.AsNoTracking()
            .Where(s => !registeredAppIds.Contains(s.application_id))
            .OrderBy(s => s.studentName)
            .ToList();

        return Page();
    }
}
