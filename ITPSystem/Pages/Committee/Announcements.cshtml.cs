using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class CommitteeAnnouncementsModel : CommitteePageModelBase
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

    public CommitteeAnnouncementsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<Announcement> Announcements { get; private set; } = new();
    public int TotalAnnouncements { get; private set; }
    public int PublishedAnnouncements { get; private set; }
    public int ScheduledAnnouncements { get; private set; }
    public int ExpiredAnnouncements { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        LoadPageData();
        return Page();
    }

    public IActionResult OnPostTogglePublish(long announcementId)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var item = _db.Announcements.FirstOrDefault(a => a.announcement_id == announcementId);
        if (item == null)
        {
            StatusMessage = "Announcement was not found.";
            return RedirectToPage();
        }

        item.is_published = !item.is_published;
        if (item.is_published && !item.publish_at.HasValue)
        {
            item.publish_at = DateTime.Now;
        }

        _db.SaveChanges();
        StatusMessage = item.is_published ? "Announcement published." : "Announcement unpublished.";
        return RedirectToPage();
    }

    public IActionResult OnPostDelete(long announcementId)
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var item = _db.Announcements.FirstOrDefault(a => a.announcement_id == announcementId);
        if (item == null)
        {
            StatusMessage = "Announcement was not found.";
            return RedirectToPage();
        }

        _db.Announcements.Remove(item);
        _db.SaveChanges();
        StatusMessage = "Announcement deleted.";
        return RedirectToPage();
    }

    private void LoadPageData()
    {
        var now = DateTime.Now;

        Announcements = _db.Announcements.AsNoTracking()
            .OrderByDescending(a => a.publish_at ?? a.created_at)
            .ThenByDescending(a => a.created_at)
            .ToList();
        TotalAnnouncements = Announcements.Count;
        PublishedAnnouncements = Announcements.Count(a => a.is_published);
        ScheduledAnnouncements = Announcements.Count(a => a.publish_at.HasValue && a.publish_at.Value > now);
        ExpiredAnnouncements = Announcements.Count(a => a.expire_at.HasValue && a.expire_at.Value < now);
    }
}
