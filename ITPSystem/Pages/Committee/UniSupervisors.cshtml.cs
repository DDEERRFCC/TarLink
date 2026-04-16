using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

public class CommitteeUniSupervisorsModel : CommitteePageModelBase
{
    private static readonly string[] AllowedFaculties =
    {
        "Faculty of Computing and Information Technology",
        "Faculty of Engineering and Technology",
        "Faculty of Business and Finance",
        "Faculty of Accountancy, Finance and Business",
        "Faculty of Social Science and Humanities",
        "Faculty of Built Environment"
    };

    private static readonly string[] AllowedCampuses =
    {
        "Kuala Lumpur Main Campus",
        "Penang Branch Campus",
        "Perak Branch Campus",
        "Johor Branch Campus",
        "Sabah Branch Campus",
        "Sarawak Branch Campus"
    };

    private readonly ApplicationDbContext _db;

    public CommitteeUniSupervisorsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<SupervisorItem> Supervisors { get; private set; } = new();
    [TempData]
    public string? StatusMessage { get; set; }

    public IActionResult OnGet(string? editId = null)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        LoadData();
        if (!string.IsNullOrWhiteSpace(editId))
        {
            return RedirectToPage("/Committee/EditSupervisor", new { staffId = editId });
        }

        return Page();
    }

    public IActionResult OnPostDelete(string staffId)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var existing = _db.UcSupervisors.FirstOrDefault(x => x.staffId == staffId);
        if (existing == null)
        {
            StatusMessage = "Supervisor record was not found.";
            return RedirectToPage();
        }

        _db.UcSupervisors.Remove(existing);
        _db.SaveChanges();

        StatusMessage = "University supervisor deleted successfully.";
        return RedirectToPage();
    }

    private void LoadData()
    {
        Supervisors = _db.UcSupervisors.AsNoTracking()
            .OrderBy(x => x.name)
            .Select(x => new SupervisorItem
            {
                staffId = x.staffId,
                name = x.name,
                email = x.email,
                contact = x.contact,
                remark = x.remark,
                faculty = x.faculty,
                campus = x.campus,
                isActive = x.isActive,
                isCommittee = x.isCommittee,
                created_at = x.created_at,
                StudentCount = _db.StudentApplications.Count(s =>
                    (!string.IsNullOrWhiteSpace(x.email) && s.ucSupervisorEmail == x.email)
                    || (string.IsNullOrWhiteSpace(s.ucSupervisorEmail) && s.ucSupervisor == x.name))
            })
            .ToList();
    }

    public class SupervisorItem
    {
        public string staffId { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string? email { get; set; }
        public string? contact { get; set; }
        public string? remark { get; set; }
        public string? faculty { get; set; }
        public string? campus { get; set; }
        public bool isActive { get; set; }
        public bool isCommittee { get; set; }
        public DateTime created_at { get; set; }
        public int StudentCount { get; set; }
    }

}
