using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITPSystem.Models
{
    [Table("ucsupervisor")]
    public class UcSupervisor
    {
        [Key]
        [StringLength(16)]
        public string staffId { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string name { get; set; } = string.Empty;

        [StringLength(250)]
        public string? email { get; set; }

        [StringLength(20)]
        public string? contact { get; set; }

        [StringLength(255)]
        public string? password { get; set; }

        [StringLength(150)]
        public string? remark { get; set; }

        public bool isActive { get; set; } = true;

        public bool isCommittee { get; set; }

        [StringLength(100)]
        public string? faculty { get; set; }

        [StringLength(100)]
        public string? campus { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime created_at { get; set; }
    }
}
