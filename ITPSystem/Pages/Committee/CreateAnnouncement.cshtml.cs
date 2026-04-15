using System.ComponentModel.DataAnnotations;
using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class CommitteeCreateAnnouncementModel : CommitteePageModelBase
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

    public CommitteeCreateAnnouncementModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<Cohort> Cohorts { get; private set; } = new();
    public IReadOnlyList<string> Faculties { get; } = AllowedFaculties;
    public IReadOnlyList<string> Campuses { get; } = AllowedCampuses;

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
        if (!Input.PublishAt.HasValue)
        {
            Input.PublishAt = DateTime.Now;
        }
        return Page();
    }

    public IActionResult OnPost()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        Input.Title = (Input.Title ?? string.Empty).Trim();
        Input.Message = (Input.Message ?? string.Empty).Trim();
        Input.TargetRole = string.IsNullOrWhiteSpace(Input.TargetRole) ? "all" : Input.TargetRole.Trim().ToLowerInvariant();
        Input.Faculty = string.IsNullOrWhiteSpace(Input.Faculty) ? null : Input.Faculty.Trim();
        Input.Campus = string.IsNullOrWhiteSpace(Input.Campus) ? null : Input.Campus.Trim();

        if (Input.PublishAt.HasValue && Input.ExpireAt.HasValue && Input.ExpireAt.Value < Input.PublishAt.Value)
        {
            ModelState.AddModelError("Input.ExpireAt", "Expire date must be after publish date.");
        }

        var validRoles = new[] { "all", "student", "supervisor", "committee" };
        if (!validRoles.Contains(Input.TargetRole))
        {
            ModelState.AddModelError("Input.TargetRole", "Please choose a valid target role.");
        }

        if (!string.IsNullOrWhiteSpace(Input.Faculty) && !AllowedFaculties.Contains(Input.Faculty, StringComparer.Ordinal))
        {
            ModelState.AddModelError("Input.Faculty", "Please choose a valid faculty.");
        }

        if (!string.IsNullOrWhiteSpace(Input.Campus) && !AllowedCampuses.Contains(Input.Campus, StringComparer.Ordinal))
        {
            ModelState.AddModelError("Input.Campus", "Please choose a valid campus.");
        }

        if (!ModelState.IsValid)
        {
            LoadPageData();
            return Page();
        }

        var role = HttpContext.Session.GetString("UserRole");
        var rawUserId = HttpContext.Session.GetString("UserID");
        if (!string.Equals(role, "committee", StringComparison.OrdinalIgnoreCase) || !int.TryParse(rawUserId, out var userId))
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var announcement = new Announcement
        {
            created_by_user_id = userId,
            title = Input.Title,
            message = Input.Message,
            target_role = Input.TargetRole,
            cohort_id = Input.CohortId,
            faculty = Input.Faculty,
            campus = Input.Campus,
            is_published = Input.IsPublished,
            publish_at = Input.PublishAt,
            expire_at = Input.ExpireAt,
            created_at = DateTime.Now
        };

        _db.Announcements.Add(announcement);
        _db.SaveChanges();
        StatusMessage = "Announcement saved successfully.";
        return RedirectToPage("/Committee/Announcements");
    }

    private void LoadPageData()
    {
        Cohorts = _db.Cohorts.AsNoTracking()
            .OrderByDescending(c => c.startDate)
            .ToList();
    }
}
