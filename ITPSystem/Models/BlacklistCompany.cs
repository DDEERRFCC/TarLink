using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITPSystem.Models
{
    [Table("blacklistcompany")]
    public class BlacklistCompany
    {
        [Key]
        public int id { get; set; }

        public DateTime? created_at { get; set; }

        [StringLength(255)]
        public string? comName { get; set; }

        [StringLength(500)]
        public string? address { get; set; }

        [StringLength(500)]
        public string? reason { get; set; }

        [StringLength(255)]
        public string? byCommittee { get; set; }

        [StringLength(255)]
        public string? attachment { get; set; }

        [Column("is_removed")]
        public bool is_removed { get; set; }

        public DateTime? removed_at { get; set; }
    }
}
