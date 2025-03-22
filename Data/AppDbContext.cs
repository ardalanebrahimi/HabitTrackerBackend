using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Habit> Habits { get; set; }
    public DbSet<HabitLog> HabitLogs { get; set; }
    public DbSet<Connection> Connections { get; set; }
    public DbSet<HabitCheckRequest> HabitCheckRequests { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Required for Identity setup

        modelBuilder.Entity<User>().ToTable("users");
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        modelBuilder.Entity<Connection>().ToTable("connections");

        // Ensure Habit-Log Relationship
        modelBuilder.Entity<Habit>()
            .HasMany(h => h.Logs)
            .WithOne()
            .HasForeignKey(l => l.HabitId);


        modelBuilder.Entity<Connection>()
            .HasOne(uc => uc.RequesterUser)
            .WithMany(u => u.SentConnections)
            .HasForeignKey(uc => uc.UserId) 
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Connection>()
            .HasOne(uc => uc.ReceiverUser)
            .WithMany(u => u.ReceivedConnections)
            .HasForeignKey(uc => uc.ConnectedUserId) 
            .OnDelete(DeleteBehavior.Restrict);
    }
}
