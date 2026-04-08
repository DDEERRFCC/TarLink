using System.ComponentModel.DataAnnotations;
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
    public List<Cohort> Cohorts { get; private set; } = new();
    public IReadOnlyList<string> Faculties { get; } = AllowedFaculties;
    public IReadOnlyList<string> Campuses { get; } = AllowedCampuses;

    public int TotalAnnouncements { get; private set; }
    public int PublishedAnnouncements { get; private set; }
    public int ScheduledAnnouncements { get; private set; }
    public int ExpiredAnnouncements { get; private set; }

    [BindProperty]
    public AnnouncementInput Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class AnnouncementInput
    {
        [Required]
        [StringLength(255)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        [Required]
        public string TargetRole { get; set; } = "all";

        public int? CohortId { get; set; }
        public string? Faculty { get; set; }
        public string? Campus { get; set; }
        public bool IsPublished { get; set; } = true;
        public DateTime? PublishAt { get; set; }
        public DateTime? ExpireAt { get; set; }
    }

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        LoadPageData();
        SetDefaultPublishDate();
        return Page();
    }

    public IActionResult OnPostCreate()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        NormalizeInput();
        ValidateAnnouncementInput();

        if (!ModelState.IsValid)
        {
            LoadPageData();
            return Page();
        }

        var creatorId = GetCurrentCommitteeUserId();
        if (creatorId == null)
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var announcement = new Announcement
        {
            created_by_user_id = creatorId.Value,
            title = Input.Title.Trim(),
            message = Input.Message.Trim(),
            target_role = Input.TargetRole.Trim().ToLowerInvariant(),
            cohort_id = Input.CohortId,
            faculty = NormalizeOptional(Input.Faculty),
            campus = NormalizeOptional(Input.Campus),
            is_published = Input.IsPublished,
            publish_at = Input.PublishAt,
            expire_at = Input.ExpireAt,
            created_at = DateTime.Now
        };

        _db.Announcements.Add(announcement);
        _db.SaveChanges();

        StatusMessage = "Announcement saved successfully.";
        return RedirectToPage();
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

        Cohorts = _db.Cohorts.AsNoTracking()
            .OrderByDescending(c => c.startDate)
            .ToList();

        TotalAnnouncements = Announcements.Count;
        PublishedAnnouncements = Announcements.Count(a => a.is_published);
        ScheduledAnnouncements = Announcements.Count(a => a.publish_at.HasValue && a.publish_at.Value > now);
        ExpiredAnnouncements = Announcements.Count(a => a.expire_at.HasValue && a.expire_at.Value < now);
    }

    private void NormalizeInput()
    {
        Input.Title = (Input.Title ?? string.Empty).Trim();
        Input.Message = (Input.Message ?? string.Empty).Trim();
        Input.TargetRole = string.IsNullOrWhiteSpace(Input.TargetRole) ? "all" : Input.TargetRole.Trim().ToLowerInvariant();
        Input.Faculty = NormalizeOptional(Input.Faculty);
        Input.Campus = NormalizeOptional(Input.Campus);
    }

    private void ValidateAnnouncementInput()
    {
        if (Input.PublishAt.HasValue && Input.ExpireAt.HasValue && Input.ExpireAt.Value < Input.PublishAt.Value)
        {
            ModelState.AddModelError("Input.ExpireAt", "Expire date must be after publish date.");
        }

        var validRoles = new[] { "all", "student", "supervisor", "committee" };
        if (!validRoles.Contains(Input.TargetRole))
        {
            ModelState.AddModelError("Input.TargetRole", "Please choose a valid target role.");
        }

        if (!string.IsNullOrWhiteSpace(Input.Faculty) &&
            !AllowedFaculties.Contains(Input.Faculty, StringComparer.Ordinal))
        {
            ModelState.AddModelError("Input.Faculty", "Please choose a valid faculty.");
        }

        if (!string.IsNullOrWhiteSpace(Input.Campus) &&
            !AllowedCampuses.Contains(Input.Campus, StringComparer.Ordinal))
        {
            ModelState.AddModelError("Input.Campus", "Please choose a valid campus.");
        }
    }

    private void SetDefaultPublishDate()
    {
        if (!Input.PublishAt.HasValue)
        {
            Input.PublishAt = DateTime.Now;
        }
    }

    private int? GetCurrentCommitteeUserId()
    {
        var role = HttpContext.Session.GetString("UserRole");
        var rawUserId = HttpContext.Session.GetString("UserID");

        if (!string.Equals(role, "committee", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return int.TryParse(rawUserId, out var userId) ? userId : null;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
