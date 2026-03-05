using ITPSystem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class CommitteeUniSupervisorsModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeUniSupervisorsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<SupervisorItem> Supervisors { get; private set; } = new();

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        Supervisors = _db.StudentApplications.AsNoTracking()
            .Where(s => !string.IsNullOrWhiteSpace(s.ucSupervisor) || !string.IsNullOrWhiteSpace(s.ucSupervisorEmail))
            .GroupBy(s => new { s.ucSupervisor, s.ucSupervisorEmail })
            .Select(g => new SupervisorItem
            {
                Name = g.Key.ucSupervisor ?? "-",
                Email = g.Key.ucSupervisorEmail ?? "-",
                StudentCount = g.Count()
            })
            .OrderBy(x => x.Name)
            .ToList();

        return Page();
    }

    public class SupervisorItem
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int StudentCount { get; set; }
    }
}
