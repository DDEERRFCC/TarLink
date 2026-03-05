using ITPSystem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class CommitteeDashboardModel : CommitteePageModelBase
{
    private readonly ApplicationDbContext _db;

    public CommitteeDashboardModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public int TotalStudents { get; private set; }
    public int PendingStudents { get; private set; }
    public int ApprovedStudents { get; private set; }
    public int TotalCompanies { get; private set; }
    public int SubmittedReports { get; private set; }
    public int PendingReports { get; private set; }

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        TotalStudents = _db.StudentApplications.Count();
        PendingStudents = _db.StudentApplications.Count(s => s.applyStatus == "pending");
        ApprovedStudents = _db.StudentApplications.Count(s => s.applyStatus == "approved");
        TotalCompanies = _db.Companies.Count();
        SubmittedReports = _db.ProgressReports.Count(r => r.status == 1);
        PendingReports = _db.ProgressReports.Count(r => r.status == 0);

        return Page();
    }
}
