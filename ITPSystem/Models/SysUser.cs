using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITPSystem.Models
{
    [Table("sysuser")]
    public class SysUser
    {
        [Key]
        public int user_id { get; set; }

        [Required]
        [StringLength(255)]
        public string email { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string username { get; set; } = string.Empty;

        [Required]
        [Column("password")]
        public string password { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string role { get; set; } = "student"; // enum-safe

        [StringLength(20)]
        public string? ic_number { get; set; }

        public int? application_id { get; set; }

        public bool is_active { get; set; } = true;

        public bool is_locked { get; set; } = false;

        public byte login_attempts { get; set; } = 0;

        public DateTime? last_login_at { get; set; }

        public DateTime? password_changed_at { get; set; }

        [StringLength(100)]
        public string? password_reset_token { get; set; }

        public DateTime? password_reset_expires { get; set; }

        public DateTime? email_verified_at { get; set; }

        public DateTime created_at { get; set; } = DateTime.Now;

        public DateTime updated_at { get; set; }

        // 🔗 Navigation Property
        [ForeignKey("application_id")]
        public StudentApplication? StudentApplication { get; set; }
    }
}
