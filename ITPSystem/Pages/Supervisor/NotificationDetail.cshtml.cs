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
            if (!TryGetSupervisorContext(out var supervisorStaffId))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            LoadNotification(supervisorStaffId);
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

        private void LoadNotification(string supervisorStaffId)
        {
            Notification = _db.Notifications
                .FirstOrDefault(n => n.notification_id == notificationId && n.from_user_id == supervisorStaffId);

            if (Notification != null)
            {
                var sender = _db.UcSupervisors.AsNoTracking()
                    .FirstOrDefault(u => u.staffId == Notification.from_user_id);
                SenderName = sender != null 
                    ? sender.name
                    : "System";
            }
        }

        private bool TryGetSupervisorContext(out string supervisorStaffId)
        {
            supervisorStaffId = string.Empty;
            var role = (HttpContext.Session.GetString("UserRole") ?? string.Empty).ToLowerInvariant();
            supervisorStaffId = HttpContext.Session.GetString("UserID") ?? string.Empty;
            return role == "supervisor" && !string.IsNullOrWhiteSpace(supervisorStaffId);
        }
    }
}
