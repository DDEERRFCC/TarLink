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
        public DbSet<Cohort> Cohorts { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<AssessmentMark> AssessmentMarks { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<DocumentReview> DocumentReviews { get; set; }
        public DbSet<ProgressReport> ProgressReports { get; set; }
        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<StudentCv> StudentCvs { get; set; }
        public DbSet<CompanyRequest> CompanyRequests { get; set; }




    }

}
