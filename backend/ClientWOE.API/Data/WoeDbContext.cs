using ClientWOE.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ClientWOE.API.Data;

public class WoeDbContext : DbContext
{
    // HasData() needs fixed values (no DateTime.UtcNow), otherwise every migration would "change" the seed rows.
    private static readonly DateTime SeedDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public WoeDbContext(DbContextOptions<WoeDbContext> options) : base(options)
    {
    }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<TestMaster> TestMasters => Set<TestMaster>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<WorkOrderTestDetail> WorkOrderTestDetails => Set<WorkOrderTestDetail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        //  Client 
        modelBuilder.Entity<Client>(e =>
        {
            e.ToTable("Client");
            e.HasIndex(x => x.ClientCode).IsUnique();
        });

        //  Patient 
        modelBuilder.Entity<Patient>(e =>
        {
            e.ToTable("Patient");
            e.HasIndex(x => x.PatientCode).IsUnique();
            e.HasIndex(x => x.MobileNumber);
            e.HasOne(x => x.Client)
                .WithMany(c => c.Patients)
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // TestMaster 
        modelBuilder.Entity<TestMaster>(e =>
        {
            e.ToTable("TestMaster");
            e.HasIndex(x => x.TestCode).IsUnique();
            e.Property(x => x.Rate).HasPrecision(18, 2);
        });

        // WorkOrder 
        // Client and Patient FKs are Restrict (not Cascade): SQL Server rejects multiple cascade paths.
        modelBuilder.Entity<WorkOrder>(e =>
        {
            e.ToTable("WorkOrder");
            e.HasIndex(x => x.WoeNumber).IsUnique();
            e.HasIndex(x => x.OrderDate);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.HasOne(x => x.Client)
                .WithMany(c => c.WorkOrders)
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Patient)
                .WithMany(p => p.WorkOrders)
                .HasForeignKey(x => x.PatientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        //  WorkOrderTestDetail 
        modelBuilder.Entity<WorkOrderTestDetail>(e =>
        {
            e.ToTable("WorkOrderTestDetail");
            e.Property(x => x.Rate).HasPrecision(18, 2);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasOne(x => x.WorkOrder)
                .WithMany(w => w.Details)
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Test)
                .WithMany(t => t.WorkOrderDetails)
                .HasForeignKey(x => x.TestId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        //  Seed data (keep in sync with database/schema.sql) 
        modelBuilder.Entity<Client>().HasData(
            SeedClient(1, "CL-WALKIN", "Walk-in Client", null),
            SeedClient(2, "CL-APOLLO", "Apollo Diagnostics", "Bangalore, Karnataka"),
            SeedClient(3, "CL-SUNRISE", "Sunrise Multispeciality Hospital", "Bangalore, Karnataka"),
            SeedClient(4, "CL-MEDLIFE", "MedLife Health Clinic", "Chennai, Tamil Nadu"),
            SeedClient(5, "CL-GREENX", "Green Cross Polyclinic", "Coimbatore, Tamil Nadu"));

        modelBuilder.Entity<TestMaster>().HasData(
            SeedTest(1, "CBC", "Complete Blood Count", "Haematology", 350m),
            SeedTest(2, "LIPID", "Lipid Profile", "Biochemistry", 600m),
            SeedTest(3, "LFT", "Liver Function Test", "Biochemistry", 700m),
            SeedTest(4, "KFT", "Kidney Function Test", "Biochemistry", 650m),
            SeedTest(5, "THYROID", "Thyroid Profile (T3, T4, TSH)", "Endocrinology", 550m),
            SeedTest(6, "HBA1C", "HbA1c", "Diabetes", 450m),
            SeedTest(7, "FBS", "Fasting Blood Sugar", "Diabetes", 120m),
            SeedTest(8, "PPBS", "Post-prandial Blood Sugar", "Diabetes", 120m),
            SeedTest(9, "VITD", "Vitamin D (25-OH)", "Vitamins", 1200m),
            SeedTest(10, "VITB12", "Vitamin B12", "Vitamins", 900m),
            SeedTest(11, "URINE", "Urine Routine", "Urine", 150m),
            SeedTest(12, "ESR", "ESR", "Haematology", 100m),
            SeedTest(13, "CRP", "C-Reactive Protein (CRP)", "Immunology", 400m),
            SeedTest(14, "CREAT", "Serum Creatinine", "Biochemistry", 200m),
            SeedTest(15, "ELECT", "Serum Electrolytes", "Biochemistry", 500m));
    }

    private static Client SeedClient(int id, string code, string name, string? address) => new()
    {
        ClientId = id,
        ClientCode = code,
        ClientName = name,
        Address = address,
        IsActive = true,
        CreatedAt = SeedDate
    };

    private static TestMaster SeedTest(int id, string code, string name, string category, decimal rate) => new()
    {
        TestId = id,
        TestCode = code,
        TestName = name,
        Category = category,
        Rate = rate,
        IsActive = true
    };
}
