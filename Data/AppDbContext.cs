using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public new DbSet<User> Users { get; set; }
    public DbSet<Habit> Habits { get; set; }
    public DbSet<HabitLog> HabitLogs { get; set; }
    public DbSet<Connection> Connections { get; set; }
    public DbSet<HabitCheckRequest> HabitCheckRequests { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Cheer> Cheers { get; set; }
    public DbSet<HabitCopy> HabitCopies { get; set; }
    
    // Subscription and Token System
    public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
    public DbSet<UserSubscription> UserSubscriptions { get; set; }
    public DbSet<TokenTransaction> TokenTransactions { get; set; }
    public DbSet<TokenPurchase> TokenPurchases { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Required for Identity setup

        modelBuilder.Entity<User>().ToTable("users");
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        modelBuilder.Entity<Connection>().ToTable("connections");
        modelBuilder.Entity<Notification>().ToTable("notifications");
        modelBuilder.Entity<Cheer>().ToTable("cheers");

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

        // Configure Cheer relationships
        modelBuilder.Entity<Cheer>()
            .HasOne(c => c.Habit)
            .WithMany()
            .HasForeignKey(c => c.HabitId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Cheer>()
            .HasOne(c => c.FromUser)
            .WithMany()
            .HasForeignKey(c => c.FromUserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        modelBuilder.Entity<Cheer>()
            .HasOne(c => c.ToUser)
            .WithMany()
            .HasForeignKey(c => c.ToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Ensure Habit-Log Relationship
        modelBuilder.Entity<Habit>()
            .HasMany(h => h.Logs)
            .WithOne()
            .HasForeignKey(l => l.HabitId);

        // Configure HabitCopy relationships
        modelBuilder.Entity<HabitCopy>()
            .HasOne(hc => hc.OriginalHabit)
            .WithMany()
            .HasForeignKey(hc => hc.OriginalHabitId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HabitCopy>()
            .HasOne(hc => hc.CopiedHabit)
            .WithMany()
            .HasForeignKey(hc => hc.CopiedHabitId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Configure User referral relationships
        modelBuilder.Entity<User>()
            .HasOne(u => u.ReferredByUser)
            .WithMany(u => u.ReferredUsers)
            .HasForeignKey(u => u.ReferredById)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Configure subscription relationships
        modelBuilder.Entity<UserSubscription>()
            .HasOne(us => us.User)
            .WithMany()
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<UserSubscription>()
            .HasOne(us => us.SubscriptionPlan)
            .WithMany()
            .HasForeignKey(us => us.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Configure token transaction relationships
        modelBuilder.Entity<TokenTransaction>()
            .HasOne(tt => tt.User)
            .WithMany()
            .HasForeignKey(tt => tt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<TokenPurchase>()
            .HasOne(tp => tp.User)
            .WithMany()
            .HasForeignKey(tp => tp.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
