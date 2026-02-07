using Microsoft.EntityFrameworkCore;
using ITPSystem.Models;

namespace ITPSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Add these DbSets
        public DbSet<StudentApplication> StudentApplications { get; set; }
        public DbSet<SysUser> SysUsers { get; set; }
    }
}
