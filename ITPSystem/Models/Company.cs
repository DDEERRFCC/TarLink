using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITPSystem.Models
{
    [Table("company")]
    public class Company
    {
        [Key]
        public int company_id { get; set; }

        public DateTime created_at { get; set; } = DateTime.Now;
        public DateTime lastUpdate { get; set; } = DateTime.Now;
        public DateTime? lastVisit { get; set; }
        public DateTime? lastContact { get; set; }

        [StringLength(15)]
        public string? regNo { get; set; }

        [StringLength(15)]
        public string? vacancyLevel { get; set; }

        [StringLength(250)]
        public string name { get; set; } = string.Empty;

        [StringLength(255)]
        public string? address1 { get; set; }

        [StringLength(255)]
        public string? address2 { get; set; }

        [StringLength(255)]
        public string? address3 { get; set; }

        public int? totalNoOfStaff { get; set; }

        [StringLength(150)]
        public string? industryInvolved { get; set; }

        [StringLength(150)]
        public string? productsAndServices { get; set; }

        [StringLength(255)]
        public string? companyBackground { get; set; }

        public byte[]? logo { get; set; }

        [StringLength(100)]
        public string? website { get; set; }

        public byte[]? ssmCert { get; set; }

        public byte? status { get; set; }

        public byte? visibility { get; set; }

        [StringLength(500)]
        public string? remark { get; set; }
    }
}
