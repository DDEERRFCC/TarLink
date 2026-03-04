using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITPSystem.Models
{
    [Table("announcement")]
    public class Announcement
    {
        [Key]
        public long announcement_id { get; set; }

        public int created_by_user_id { get; set; }

        [Required]
        [StringLength(255)]
        public string title { get; set; } = string.Empty;

        [Required]
        public string message { get; set; } = string.Empty;

        [StringLength(20)]
        public string target_role { get; set; } = "all";

        public int? cohort_id { get; set; }

        [StringLength(45)]
        public string? faculty { get; set; }

        [StringLength(45)]
        public string? campus { get; set; }

        public bool is_published { get; set; } = true;

        public DateTime? publish_at { get; set; }
        public DateTime? expire_at { get; set; }
        public DateTime created_at { get; set; }
    }
}
