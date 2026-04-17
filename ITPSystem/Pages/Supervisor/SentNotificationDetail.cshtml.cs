using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ITPSystem.Pages.Supervisor
{
    public class SentNotificationDetailModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public SentNotificationDetailModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public Notification? Notification { get; set; }
        public List<NotificationReceiver> Receivers { get; set; } = new();
        public string Message { get; set; } = string.Empty;
        public bool IsSuccess { get; set; } = false;

        [BindProperty(SupportsGet = true)]
        public long notificationId { get; set; }

        [BindProperty]
        public string Title { get; set; } = string.Empty;

        [BindProperty]
        public string NotificationMessage { get; set; } = string.Empty;

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

            LoadReceivers();
            return Page();
        }

        public IActionResult OnPostUpdate()
        {
            if (!TryGetSupervisorContext(out var supervisorStaffId))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            var notification = _db.Notifications.FirstOrDefault(n => n.notification_id == notificationId && n.from_user_id == supervisorStaffId);
            if (notification == null)
            {
                Message = "Notification not found.";
                IsSuccess = false;
                LoadNotification(supervisorStaffId);
                LoadReceivers();
                return Page();
            }

            // Validate inputs
            if (string.IsNullOrWhiteSpace(Title))
            {
                Message = "Please enter a title.";
                IsSuccess = false;
                LoadNotification(supervisorStaffId);
                LoadReceivers();
                return Page();
            }

            if (string.IsNullOrWhiteSpace(NotificationMessage))
            {
                Message = "Please enter a message.";
                IsSuccess = false;
                LoadNotification(supervisorStaffId);
                LoadReceivers();
                return Page();
            }

            try
            {
                // Update all related notifications (same content and time)
                var relatedNotifications = _db.Notifications
                    .Where(n => n.from_user_id == notification.from_user_id
                             && n.title == notification.title
                             && n.message == notification.message
                             && n.type == notification.type
                             && n.created_at >= notification.created_at.AddMinutes(-1)
                             && n.created_at <= notification.created_at.AddMinutes(1))
                    .ToList();

                foreach (var n in relatedNotifications)
                {
                    n.title = Title.Trim();
                    n.message = NotificationMessage.Trim();
                }

                _db.SaveChanges();

                Message = "Notification updated successfully.";
                IsSuccess = true;

                LoadNotification(supervisorStaffId);
                LoadReceivers();
                return Page();
            }
            catch (Exception ex)
            {
                Message = $"Error updating notification: {ex.Message}";
                IsSuccess = false;
                LoadNotification(supervisorStaffId);
                LoadReceivers();
                return Page();
            }
        }

        public IActionResult OnPostDelete()
        {
            if (!TryGetSupervisorContext(out var supervisorStaffId))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            var notification = _db.Notifications.FirstOrDefault(n => n.notification_id == notificationId && n.from_user_id == supervisorStaffId);
            if (notification == null)
            {
                Message = "Notification not found.";
                IsSuccess = false;
                return RedirectToPage("/Supervisor/Notifications");
            }

            try
            {
                // Delete all related notifications (same content and time)
                var relatedNotifications = _db.Notifications
                    .Where(n => n.from_user_id == notification.from_user_id
                             && n.title == notification.title
                             && n.message == notification.message
                             && n.type == notification.type
                             && n.created_at >= notification.created_at.AddMinutes(-1)
                             && n.created_at <= notification.created_at.AddMinutes(1))
                    .ToList();

                _db.Notifications.RemoveRange(relatedNotifications);
                _db.SaveChanges();

                TempData["Message"] = "Notification deleted successfully.";
                TempData["IsSuccess"] = "true";
                return RedirectToPage("/Supervisor/Notifications");
            }
            catch (Exception ex)
            {
                Message = $"Error deleting notification: {ex.Message}";
                IsSuccess = false;
                LoadNotification(supervisorStaffId);
                LoadReceivers();
                return Page();
            }
        }

        private void LoadNotification(string supervisorStaffId)
        {
            Notification = _db.Notifications
                .FirstOrDefault(n => n.notification_id == notificationId && n.from_user_id == supervisorStaffId);
        }

        private void LoadReceivers()
        {
            if (Notification == null)
                return;

            // Get all notifications with the same title, type, and message sent by this supervisor
            // This assumes that when sending to multiple students, they all get the same notification content
            var relatedNotifications = _db.Notifications.AsNoTracking()
                .Where(n => n.from_user_id == Notification.from_user_id
                         && n.title == Notification.title
                         && n.message == Notification.message
                         && n.type == Notification.type
                         && n.created_at >= Notification.created_at.AddMinutes(-1)
                         && n.created_at <= Notification.created_at.AddMinutes(1))
                .ToList();

            var receiverIds = relatedNotifications.Select(n => n.to_user_id).Distinct().ToList();
            var receivers = _db.SysUsers.AsNoTracking()
                .Where(u => receiverIds.Contains(u.user_id) && u.role == "student")
                .ToDictionary(u => u.user_id);

            var receiverEmails = receivers.Values.Select(u => u.email).ToList();
            var studentApps = _db.StudentApplications.AsNoTracking()
                .Where(s => receiverEmails.Contains(s.studentEmail))
                .ToDictionary(s => s.studentEmail);

            Receivers = relatedNotifications.Select(n => {
                var user = receivers.ContainsKey(n.to_user_id) ? receivers[n.to_user_id] : null;
                var student = user != null && studentApps.ContainsKey(user.email) ? studentApps[user.email] : null;

                return new NotificationReceiver
                {
                    UserId = n.to_user_id,
                    StudentId = student?.studentID ?? "Unknown",
                    StudentName = student?.studentName ?? user?.username ?? "Unknown",
                    StudentEmail = user?.email ?? "Unknown",
                    IsRead = n.is_read
                };
            }).Distinct().ToList();
        }

        private bool TryGetSupervisorContext(out string supervisorStaffId)
        {
            supervisorStaffId = string.Empty;
            var role = (HttpContext.Session.GetString("UserRole") ?? string.Empty).ToLowerInvariant();
            supervisorStaffId = HttpContext.Session.GetString("UserID") ?? string.Empty;
            return role == "supervisor" && !string.IsNullOrWhiteSpace(supervisorStaffId);
        }
    }

    public class NotificationReceiver
    {
        public int UserId { get; set; }
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;
        public bool IsRead { get; set; }
    }
}
