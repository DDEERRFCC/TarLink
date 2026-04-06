using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITPSystem.Models
{
    [Table("person")]
    public class Person
    {
        [Key]
        [Column("person_id")]
        public int person_id { get; set; }

        [Column("full_name")]
        public string full_name { get; set; } = string.Empty;

        [Column("email")]
        public string? email { get; set; }

        [Column("phone")]
        public string? phone { get; set; }

        [Column("role")]
        public string? role { get; set; }

        [Column("created_at")]
        public DateTime created_at { get; set; }
    }
}
