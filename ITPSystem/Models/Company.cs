using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITPSystem.Models
{
    [Table("company")]
    public class Company
    {
        [Key]
        public int company_id { get; set; }

        [Required]
        [StringLength(15)]
        [Column("reg_no")]
        public string regNo { get; set; } = string.Empty;

        [StringLength(250)]
        public string name { get; set; } = string.Empty;

        [Column("industry_involved")]
        [StringLength(150)]
        public string? industryInvolved { get; set; }

        [Column("products_and_services")]
        [StringLength(150)]
        public string? productsAndServices { get; set; }

        [Column("company_background")]
        public string? companyBackground { get; set; }

        public byte[]? logo { get; set; }

        [StringLength(255)]
        public string? website { get; set; }

        [Column("ssm_cert")]
        public byte[]? ssmCert { get; set; }

        public DateTime created_at { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime updated_at { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? remark { get; set; }

        public ICollection<CompanyBranch> Branches { get; set; } = new List<CompanyBranch>();
    }
}
