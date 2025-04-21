using Microsoft.EntityFrameworkCore;

namespace Randevu.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }
        public DbSet<ActionTrack> ActionTracks { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<GuestCustomer> GuestCustomers { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<WeeklyAvailability> WeeklyAvailabilities { get; set; }
        public DbSet<SpecialDay> SpecialDays { get; set; }
        public DbSet<GelirGider> GelirGiders { get; set; }
        public DbSet<Staff> Staffs { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<BlogPost> BlogPosts { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<StaffRoleRelation> StaffRoleRelations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Staff>()
                .HasOne(s => s.Role)
                .WithMany()
                .HasForeignKey(s => s.RoleID);

            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Customer)
                .WithMany()
                .HasForeignKey(a => a.CustomerID);

            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Staff)
                .WithMany()
                .HasForeignKey(a => a.StaffID);

            modelBuilder.Entity<Customer>()
                .Property(c => c.Email)
                .IsRequired();

            modelBuilder.Entity<Customer>()
                .HasIndex(c => c.Email)
                .IsUnique();

            modelBuilder.Entity<Service>()
                .Property(s => s.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<StaffRoleRelation>()
                .HasKey(srr => new { srr.StaffID, srr.RoleID }); // Çoktan çoğlu ilişki için birincil anahtarı belirtmek gerekiyor

            modelBuilder.Entity<StaffRoleRelation>()
                .HasOne(srr => srr.Staff)
                .WithMany()
                .HasForeignKey(srr => srr.StaffID)
                .OnDelete(DeleteBehavior.NoAction); // Silme işleminde hiçbir eylem yapılmayacak

            modelBuilder.Entity<StaffRoleRelation>()
                .HasOne(srr => srr.Role)
                .WithMany()
                .HasForeignKey(srr => srr.RoleID)
                .OnDelete(DeleteBehavior.NoAction); // Silme işleminde hiçbir eylem yapılmayacak
        }


    }
}
