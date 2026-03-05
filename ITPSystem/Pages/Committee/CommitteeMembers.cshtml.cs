using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class CommitteeMembersModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeMembersModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<SysUser> Members { get; private set; } = new();

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        Members = _db.SysUsers.AsNoTracking()
            .Where(u => u.role == "committee")
            .OrderBy(u => u.username)
            .ToList();

        return Page();
    }
}
