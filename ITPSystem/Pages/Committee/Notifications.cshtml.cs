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
    public int TotalNotifications { get; private set; }
    public int UnreadNotifications { get; private set; }
    public int StudentRecipients { get; private set; }
    public int SupervisorRecipients { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

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
        TotalNotifications = _db.Notifications.Count();
        UnreadNotifications = _db.Notifications.Count(n => !n.is_read);
        StudentRecipients = _db.SysUsers.Count(u => u.role == "student" && u.is_active);
        SupervisorRecipients = _db.SysUsers.Count(u => u.role == "supervisor" && u.is_active);
    }
}
