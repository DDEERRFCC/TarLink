using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ITPSystem.Pages.Supervisor
{
    public class NotificationsModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public NotificationsModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public List<NotificationItem> Items { get; set; } = new();
        public List<NotificationItem> SentNotifications { get; set; } = new();

        public IActionResult OnGet()
        {
            if (!TryGetUserId(out var userId))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            // Handle TempData messages
            if (TempData.ContainsKey("Message"))
            {
                ViewData["Message"] = TempData["Message"];
                ViewData["IsSuccess"] = TempData["IsSuccess"];
            }

            LoadReceivedNotifications(userId);
            LoadSentNotifications(userId);
            return Page();
        }

        public IActionResult OnPostMarkAllRead()
        {
            if (!TryGetUserId(out var userId))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            var unread = _db.Notifications.Where(n => n.to_user_id == userId && !n.is_read).ToList();
            foreach (var item in unread)
            {
                item.is_read = true;
            }
            _db.SaveChanges();
            return RedirectToPage();
        }

        private void LoadReceivedNotifications(int userId)
        {
            var notifications = _db.Notifications.AsNoTracking()
                .Where(n => n.to_user_id == userId)
                .OrderByDescending(n => n.created_at)
                .ToList();

            // Load sender information for each notification
            var senderIds = notifications.Select(n => n.from_user_id).Distinct().ToList();
            var senders = _db.SysUsers.AsNoTracking()
                .Where(u => senderIds.Contains(u.user_id))
                .ToDictionary(u => u.user_id);

            Items = notifications.Select(n => new NotificationItem
            {
                notification_id = n.notification_id,
                from_user_id = n.from_user_id,
                to_user_id = n.to_user_id,
                type = n.type,
                title = n.title,
                message = n.message,
                is_read = n.is_read,
                created_at = n.created_at,
                SenderName = senders.ContainsKey(n.from_user_id) 
                    ? (senders[n.from_user_id].username ?? senders[n.from_user_id].email ?? "System")
                    : "System"
            }).ToList();
        }

        private void LoadSentNotifications(int userId)
        {
            var notifications = _db.Notifications.AsNoTracking()
                .Where(n => n.from_user_id == userId)
                .OrderByDescending(n => n.created_at)
                .ToList();

            // Group notifications by content and time to avoid duplicates for bulk sends
            var groupedNotifications = notifications
                .GroupBy(n => new { n.title, n.message, n.type, DateTime = n.created_at.Date.AddHours(n.created_at.Hour).AddMinutes(n.created_at.Minute) })
                .Select(g => g.OrderBy(n => n.created_at).First()) // Take the first one in each group
                .OrderByDescending(n => n.created_at)
                .ToList();

            SentNotifications = groupedNotifications.Select(n => new NotificationItem
            {
                notification_id = n.notification_id,
                from_user_id = n.from_user_id,
                to_user_id = n.to_user_id,
                type = n.type,
                title = n.title,
                message = n.message,
                is_read = n.is_read,
                created_at = n.created_at,
                SenderName = "You"
            }).ToList();
        }

        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var role = (HttpContext.Session.GetString("UserRole") ?? string.Empty).ToLowerInvariant();
            var rawUserId = HttpContext.Session.GetString("UserID");
            return role == "supervisor" && int.TryParse(rawUserId, out userId);
        }
    }

    public class NotificationItem
    {
        public long notification_id { get; set; }
        public int from_user_id { get; set; }
        public int to_user_id { get; set; }
        public string type { get; set; } = string.Empty;
        public string title { get; set; } = string.Empty;
        public string? message { get; set; }
        public bool is_read { get; set; }
        public DateTime created_at { get; set; }
        public string SenderName { get; set; } = "System";
    }
}


