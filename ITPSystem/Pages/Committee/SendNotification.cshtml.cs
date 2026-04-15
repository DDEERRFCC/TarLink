using System.ComponentModel.DataAnnotations;
using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class CommitteeSendNotificationModel : CommitteePageModelBase
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

    public CommitteeSendNotificationModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<Cohort> Cohorts { get; private set; } = new();
    public IReadOnlyList<string> Faculties { get; } = AllowedFaculties;
    public IReadOnlyList<string> Campuses { get; } = AllowedCampuses;

    [BindProperty]
    public NotificationInput Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class NotificationInput
    {
        [Required]
        [StringLength(60)]
        public string Type { get; set; } = "committee";

        [Required]
        [StringLength(255)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        [Required]
        public string TargetRole { get; set; } = "student";

        public int? CohortId { get; set; }
        public string? Faculty { get; set; }
        public string? Campus { get; set; }
        public string? StudentStatus { get; set; }
    }

    public IActionResult OnGet()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        LoadPageData();
        return Page();
    }

    public IActionResult OnPost()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        Input.Type = (Input.Type ?? string.Empty).Trim();
        Input.Title = (Input.Title ?? string.Empty).Trim();
        Input.Message = (Input.Message ?? string.Empty).Trim();
        Input.TargetRole = string.IsNullOrWhiteSpace(Input.TargetRole) ? "student" : Input.TargetRole.Trim().ToLowerInvariant();
        Input.Faculty = string.IsNullOrWhiteSpace(Input.Faculty) ? null : Input.Faculty.Trim();
        Input.Campus = string.IsNullOrWhiteSpace(Input.Campus) ? null : Input.Campus.Trim();
        Input.StudentStatus = string.IsNullOrWhiteSpace(Input.StudentStatus) ? null : Input.StudentStatus.Trim();

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

        var recipients = ResolveRecipients().Distinct().ToList();
        if (recipients.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "No recipients matched the selected filters.");
            LoadPageData();
            return Page();
        }

        var notifications = recipients.Select(targetId => new Notification
        {
            from_user_id = userId,
            to_user_id = targetId,
            type = Input.Type,
            title = Input.Title,
            message = Input.Message,
            is_read = false,
            created_at = DateTime.Now
        });

        _db.Notifications.AddRange(notifications);
        _db.SaveChanges();
        StatusMessage = $"Notification sent to {recipients.Count} user(s).";
        return RedirectToPage("/Committee/Notifications");
    }

    private void LoadPageData()
    {
        Cohorts = _db.Cohorts.AsNoTracking()
            .OrderByDescending(c => c.startDate)
            .ToList();
    }

    private IEnumerable<int> ResolveRecipients()
    {
        var targetRole = Input.TargetRole;
        var studentStatus = string.IsNullOrWhiteSpace(Input.StudentStatus) ? null : Input.StudentStatus.ToLowerInvariant();
        var faculty = Input.Faculty;
        var campus = Input.Campus;

        var users = _db.SysUsers.AsNoTracking()
            .Where(u => u.is_active)
            .AsQueryable();

        if (targetRole != "all")
        {
            users = users.Where(u => u.role == targetRole);
        }

        if (targetRole == "student" || targetRole == "all")
        {
            var studentUsers = from user in users
                               join application in _db.StudentApplications.AsNoTracking() on user.application_id equals application.application_id into appJoin
                               from application in appJoin.DefaultIfEmpty()
                               join cohort in _db.Cohorts.AsNoTracking() on application.cohortId equals cohort.cohort_id into cohortJoin
                               from cohort in cohortJoin.DefaultIfEmpty()
                               where user.role == "student"
                               select new
                               {
                                   user.user_id,
                                   application,
                                   cohort
                               };

            if (Input.CohortId.HasValue)
            {
                studentUsers = studentUsers.Where(x => x.application != null && x.application.cohortId == Input.CohortId.Value);
            }

            if (!string.IsNullOrWhiteSpace(studentStatus))
            {
                studentUsers = studentUsers.Where(x => x.application != null && x.application.applyStatus != null && x.application.applyStatus.ToLower() == studentStatus);
            }

            if (!string.IsNullOrWhiteSpace(faculty))
            {
                studentUsers = studentUsers.Where(x => x.cohort != null && x.cohort.faculty == faculty);
            }

            if (!string.IsNullOrWhiteSpace(campus))
            {
                studentUsers = studentUsers.Where(x => x.cohort != null && x.cohort.campus == campus);
            }

            if (targetRole == "student")
            {
                return studentUsers.Select(x => x.user_id).ToList();
            }

            var studentIds = studentUsers.Select(x => x.user_id).ToList();
            var otherIds = users.Where(u => u.role != "student").Select(u => u.user_id).ToList();
            return studentIds.Concat(otherIds);
        }

        if (targetRole == "supervisor" || targetRole == "committee")
        {
            var roleUsers = from user in users
                            join supervisor in _db.UcSupervisors.AsNoTracking() on user.email equals supervisor.email into supervisorJoin
                            from supervisor in supervisorJoin.DefaultIfEmpty()
                            where user.role == targetRole
                            select new
                            {
                                user.user_id,
                                SupervisorFaculty = supervisor != null ? supervisor.faculty : null,
                                SupervisorCampus = supervisor != null ? supervisor.campus : null
                            };

            if (!string.IsNullOrWhiteSpace(faculty))
            {
                roleUsers = roleUsers.Where(x => x.SupervisorFaculty == faculty);
            }

            if (!string.IsNullOrWhiteSpace(campus))
            {
                roleUsers = roleUsers.Where(x => x.SupervisorCampus == campus);
            }

            return roleUsers.Select(x => x.user_id).ToList();
        }

        return users.Select(u => u.user_id).ToList();
    }
}
