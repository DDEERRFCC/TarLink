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

    public List<CommitteeMemberRow> Members { get; private set; } = new();

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        Members = _db.UcSupervisors.AsNoTracking()
            .Where(s => s.isCommittee)
            .Select(s => new CommitteeMemberRow
            {
                staffId = s.staffId,
                name = s.name,
                username = s.staffId,
                email = s.email,
                is_active = s.isActive,
                is_locked = null
            })
            .OrderBy(m => m.username)
            .ToList();

        return Page();
    }

    public class CommitteeMemberRow
    {
        public string? staffId { get; set; }
        public string? name { get; set; }
        public string username { get; set; } = string.Empty;
        public string? email { get; set; }
        public bool is_active { get; set; }
        public bool? is_locked { get; set; }
    }
}
