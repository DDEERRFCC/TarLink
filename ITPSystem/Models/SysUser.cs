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

        [StringLength(255)]
        public string? email { get; set; }

        [StringLength(50)]
        public string? username { get; set; }

        [StringLength(255)]
        public string? password { get; set; }

        [StringLength(30)]
        public string? role { get; set; }

        public long? application_id { get; set; }

        [StringLength(20)]
        public string? ic_number { get; set; }

        public DateTime created_at { get; set; } = DateTime.Now;
    }
}
