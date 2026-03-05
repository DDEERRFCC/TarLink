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

        public List<Notification> Items { get; set; } = new();

        public IActionResult OnGet()
        {
            if (!TryGetUserId(out var userId))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            LoadItems(userId);
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

        private void LoadItems(int userId)
        {
            Items = _db.Notifications.AsNoTracking()
                .Where(n => n.to_user_id == userId)
                .OrderByDescending(n => n.created_at)
                .ToList();
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


