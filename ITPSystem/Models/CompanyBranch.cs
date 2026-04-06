using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITPSystem.Models
{
    [Table("companybranch")]
    public class CompanyBranch
    {
        [Key]
        public int branch_id { get; set; }

        [Required]
        public int company_id { get; set; }

        [StringLength(255)]
        public string? address_line { get; set; }

        [StringLength(100)]
        public string? city { get; set; }

        [StringLength(100)]
        public string? state { get; set; }

        [StringLength(20)]
        public string? postcode { get; set; }

        [StringLength(100)]
        public string? country { get; set; }

        [Column("total_no_of_staff")]
        public int? totalNoOfStaff { get; set; }

        [Column("vacancy_level")]
        [StringLength(15)]
        public string? vacancyLevel { get; set; }

        public byte? status { get; set; }

        public byte? visibility { get; set; }

        [Column("last_visit")]
        public DateTime? lastVisit { get; set; }

        [Column("last_contact")]
        public DateTime? lastContact { get; set; }

        [Column("is_hq")]
        public bool is_hq { get; set; }

        public DateTime created_at { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime updated_at { get; set; } = DateTime.Now;

        [ForeignKey(nameof(company_id))]
        public Company? Company { get; set; }
    }
}
