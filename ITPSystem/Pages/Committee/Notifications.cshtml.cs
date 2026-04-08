using System.ComponentModel.DataAnnotations;
using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class CommitteeNotificationsModel : CommitteePageModelBase
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

    public CommitteeNotificationsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<NotificationViewRow> RecentNotifications { get; private set; } = new();
    public List<Cohort> Cohorts { get; private set; } = new();
    public IReadOnlyList<string> Faculties { get; } = AllowedFaculties;
    public IReadOnlyList<string> Campuses { get; } = AllowedCampuses;

    public int TotalNotifications { get; private set; }
    public int UnreadNotifications { get; private set; }
    public int StudentRecipients { get; private set; }
    public int SupervisorRecipients { get; private set; }

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

    public class NotificationViewRow
    {
        public DateTime CreatedAt { get; set; }
        public string ToUser { get; set; } = string.Empty;
        public string ToRole { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Message { get; set; }
        public bool IsRead { get; set; }
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

    public IActionResult OnPostSend()
    {
        if (!IsCommittee())
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        NormalizeInput();
        ValidateNotificationInput();

        if (!ModelState.IsValid)
        {
            LoadPageData();
            return Page();
        }

        var senderId = GetCurrentCommitteeUserId();
        if (senderId == null)
        {
            return RedirectToPage("/Login/CommitteeLogin");
        }

        var recipients = ResolveRecipients()
            .Distinct()
            .ToList();

        if (recipients.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "No recipients matched the selected filters.");
            LoadPageData();
            return Page();
        }

        var notifications = recipients.Select(userId => new Notification
        {
            from_user_id = senderId.Value,
            to_user_id = userId,
            type = Input.Type.Trim(),
            title = Input.Title.Trim(),
            message = Input.Message.Trim(),
            is_read = false,
            created_at = DateTime.Now
        });

        _db.Notifications.AddRange(notifications);
        _db.SaveChanges();

        StatusMessage = $"Notification sent to {recipients.Count} user(s).";
        return RedirectToPage();
    }

    private void LoadPageData()
    {
        RecentNotifications = (
                from notification in _db.Notifications.AsNoTracking()
                join user in _db.SysUsers.AsNoTracking() on notification.to_user_id equals user.user_id
                orderby notification.created_at descending
                select new NotificationViewRow
                {
                    CreatedAt = notification.created_at,
                    ToUser = string.IsNullOrWhiteSpace(user.username) ? user.email : user.username,
                    ToRole = user.role,
                    Type = notification.type,
                    Title = notification.title,
                    Message = notification.message,
                    IsRead = notification.is_read
                })
            .Take(30)
            .ToList();

        Cohorts = _db.Cohorts.AsNoTracking()
            .OrderByDescending(c => c.startDate)
            .ToList();

        TotalNotifications = _db.Notifications.Count();
        UnreadNotifications = _db.Notifications.Count(n => !n.is_read);
        StudentRecipients = _db.SysUsers.Count(u => u.role == "student" && u.is_active);
        SupervisorRecipients = _db.SysUsers.Count(u => u.role == "supervisor" && u.is_active);
    }

    private IEnumerable<int> ResolveRecipients()
    {
        var targetRole = Input.TargetRole.Trim().ToLowerInvariant();
        var studentStatus = NormalizeOptional(Input.StudentStatus)?.ToLowerInvariant();
        var faculty = NormalizeOptional(Input.Faculty);
        var campus = NormalizeOptional(Input.Campus);

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

    private void NormalizeInput()
    {
        Input.Type = (Input.Type ?? string.Empty).Trim();
        Input.Title = (Input.Title ?? string.Empty).Trim();
        Input.Message = (Input.Message ?? string.Empty).Trim();
        Input.TargetRole = string.IsNullOrWhiteSpace(Input.TargetRole) ? "student" : Input.TargetRole.Trim().ToLowerInvariant();
        Input.Faculty = NormalizeOptional(Input.Faculty);
        Input.Campus = NormalizeOptional(Input.Campus);
        Input.StudentStatus = NormalizeOptional(Input.StudentStatus);
    }

    private void ValidateNotificationInput()
    {
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
