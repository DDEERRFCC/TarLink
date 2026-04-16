using ITPSystem.Data;
using ITPSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ITPSystem.Pages.Supervisor
{
    public class SendNotificationModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public SendNotificationModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public List<StudentApplication> AssignedStudents { get; set; } = new();
        public string Message { get; set; } = string.Empty;
        public bool IsSuccess { get; set; } = false;

        [BindProperty]
        public List<int> SelectedStudentIds { get; set; } = new();

        [BindProperty]
        public string Type { get; set; } = string.Empty;

        [BindProperty(Name = "title")]
        public string Title { get; set; } = string.Empty;

        [BindProperty(Name = "message")]
        public string NotificationMessage { get; set; } = string.Empty;

        public IActionResult OnGet()
        {
            if (!TryGetSupervisorContext(out var userId, out var userEmail))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            LoadAssignedStudents(userEmail);
            return Page();
        }

        public IActionResult OnPost()
        {
            if (!TryGetSupervisorContext(out var userId, out var userEmail))
            {
                return RedirectToPage("/Login/SupervisorLogin");
            }

            LoadAssignedStudents(userEmail);

            // Validate form inputs
            if (SelectedStudentIds.Count == 0)
            {
                Message = "Please select at least one student.";
                IsSuccess = false;
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Type))
            {
                Message = "Please select a notification type.";
                IsSuccess = false;
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Title))
            {
                Message = "Please enter a notification title.";
                IsSuccess = false;
                return Page();
            }

            if (string.IsNullOrWhiteSpace(NotificationMessage))
            {
                Message = "Please enter a notification message.";
                IsSuccess = false;
                return Page();
            }

            try
            {
                // Get the supervisor's user ID
                var supervisor = _db.SysUsers.FirstOrDefault(u => u.user_id == userId);
                if (supervisor == null)
                {
                    Message = "Supervisor account not found.";
                    IsSuccess = false;
                    return Page();
                }

                // Create notifications for each selected student
                int notificationCount = 0;
                foreach (var studentId in SelectedStudentIds)
                {
                    // Get the student application
                    var student = _db.StudentApplications
                        .FirstOrDefault(s => s.application_id == studentId);

                    if (student == null)
                        continue;

                    // Get the student's user account by email
                    var studentUser = _db.SysUsers
                        .FirstOrDefault(u => u.email == student.studentEmail && u.role == "student");

                    if (studentUser == null)
                        continue;

                    // Create notification
                    var notification = new Notification
                    {
                        from_user_id = userId,
                        to_user_id = studentUser.user_id,
                        type = Type.Trim(),
                        title = Title.Trim(),
                        message = NotificationMessage.Trim(),
                        is_read = false,
                        created_at = DateTime.UtcNow
                    };

                    _db.Notifications.Add(notification);
                    notificationCount++;
                }

                if (notificationCount == 0)
                {
                    Message = "No valid students to send notification to.";
                    IsSuccess = false;
                    return Page();
                }

                _db.SaveChanges();

                Message = $"Notification sent successfully to {notificationCount} student(s).";
                IsSuccess = true;

                // Clear form
                SelectedStudentIds.Clear();
                Type = string.Empty;
                Title = string.Empty;
                NotificationMessage = string.Empty;

                return Page();
            }
            catch (Exception ex)
            {
                Message = $"Error sending notification: {ex.Message}";
                IsSuccess = false;
                return Page();
            }
        }

        private void LoadAssignedStudents(string userEmail)
        {
            AssignedStudents = _db.StudentApplications.AsNoTracking()
                .Where(s => s.ucSupervisorEmail == userEmail || s.comSupervisorEmail == userEmail)
                .OrderBy(s => s.studentName)
                .ToList();
        }

        private bool TryGetSupervisorContext(out int userId, out string userEmail)
        {
            userId = 0;
            userEmail = string.Empty;

            var role = (HttpContext.Session.GetString("UserRole") ?? string.Empty).ToLowerInvariant();
            var rawUserId = HttpContext.Session.GetString("UserID");
            userEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;

            return role == "supervisor" 
                && int.TryParse(rawUserId, out userId) 
                && !string.IsNullOrWhiteSpace(userEmail);
        }
    }
}
