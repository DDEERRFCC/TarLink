using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ITPSystem.Pages.Supervisor
{
    public class NotificationDetailModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public NotificationDetailModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public Notification? Notification { get; set; }
        public string SenderName { get; set; } = "System";

        [BindProperty(SupportsGet = true)]
        public long notificationId { get; set; }

        public IActionResult OnGet()
        {
            if (!TryGetUserId(out var userId))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            LoadNotification(userId);
            if (Notification == null)
            {
                return NotFound();
            }

            // Automatically mark as read when viewing
            if (!Notification.is_read)
            {
                Notification.is_read = true;
                _db.SaveChanges();
            }

            return Page();
        }

        private void LoadNotification(int userId)
        {
            Notification = _db.Notifications
                .FirstOrDefault(n => n.notification_id == notificationId && n.to_user_id == userId);

            if (Notification != null)
            {
                var sender = _db.SysUsers.AsNoTracking()
                    .FirstOrDefault(u => u.user_id == Notification.from_user_id);
                SenderName = sender != null 
                    ? (sender.username ?? sender.email ?? "System")
                    : "System";
            }
        }

        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var role = (HttpContext.Session.GetString("UserRole") ?? string.Empty).ToLowerInvariant();
            var rawUserId = HttpContext.Session.GetString("UserID");
            return role == "supervisor" && int.TryParse(rawUserId, out userId);
        }
    }
}
