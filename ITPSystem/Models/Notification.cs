using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITPSystem.Models
{
    [Table("notification")]
    public class Notification
    {
        [Key]
        public long notification_id { get; set; }

        public int from_user_id { get; set; }
        public int to_user_id { get; set; }

        [StringLength(60)]
        public string type { get; set; } = string.Empty;

        [StringLength(255)]
        public string title { get; set; } = string.Empty;

        public string? message { get; set; }
        public bool is_read { get; set; } = false;
        public DateTime created_at { get; set; } = DateTime.UtcNow;
    }
}
