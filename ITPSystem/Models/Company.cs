using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITPSystem.Models
{
    [Table("company")]
    public class Company
    {
        [Key]
        public int company_id { get; set; }

        [StringLength(250)]
        public string name { get; set; } = string.Empty;

        [StringLength(255)]
        public string? address1 { get; set; }

        [StringLength(255)]
        public string? address2 { get; set; }

        [StringLength(255)]
        public string? address3 { get; set; }

        public byte? status { get; set; }

        public byte? visibility { get; set; }
    }
}
