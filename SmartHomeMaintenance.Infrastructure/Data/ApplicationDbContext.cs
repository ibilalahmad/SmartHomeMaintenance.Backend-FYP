using Microsoft.EntityFrameworkCore;
using SmartHomeMaintenance.Application.Common.Interfaces;
using SmartHomeMaintenance.Domain.Entities;
using SmartHomeMaintenance.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SmartHomeMaintenance.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext, IApplicationDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<ServiceProvider> ServiceProviders => Set<ServiceProvider>();
        public DbSet<Admin> Admins => Set<Admin>();
        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<Review> Reviews => Set<Review>();
        public DbSet<Complaint> Complaints => Set<Complaint>();
        public DbSet<BookingIssue> BookingIssues => Set<BookingIssue>();
        public DbSet<CustomerFavorite> CustomerFavorites => Set<CustomerFavorite>();
        public DbSet<AppNotification> Notifications => Set<AppNotification>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Automatically applies all IEntityTypeConfiguration classes in this assembly
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return base.SaveChangesAsync(cancellationToken);
        }

        // Automatic Seeder for Super Admin
        public static async Task SeedAdminAsync(ApplicationDbContext context)
        {
            if (!await context.Users.AnyAsync(u => u.Email == "admin@shms.com"))
            {
                var adminUser = new User
                {
                    FullName = "Bilal Ahmed (Admin)",
                    Email = "admin@shms.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                    PhoneNumber = "03001234567",
                    Address = "System HQ",
                    UserType = UserType.Admin,
                    RegDate = DateTime.UtcNow,
                    IsActive = true,
                    Admin = new Admin
                    {
                        Role = "SuperAdmin",
                        LastLogin = DateTime.UtcNow
                    }
                };

                context.Users.Add(adminUser);
                await context.SaveChangesAsync();
            }
        }

        // Automatic Seeder for Dummy Service Providers
        public static async Task SeedDummyServiceProvidersAsync(ApplicationDbContext context)
        {
            var dummySPs = new List<(string Name, string Email, string Phone, string Address, string Location, string Expertise, string Qualification, int Exp, decimal Rate, bool Approved, int DaysAgo)>
            {
                ("Tariq Mehmood", "tariq.electrician@gmail.com", "03014567890", "Model Town, Block C", "Lahore", "Electrical, AC Repair", "Diploma in Electrical Engineering (DAE)", 7, 600m, true, 85),
                ("Zahid Hussain", "zahid.plumber@gmail.com", "03219876543", "Sector F-8/2, Street 15", "Islamabad", "Plumbing, Sanitary", "Certified Plumber (TEVTA)", 9, 550m, true, 80),
                ("Kamran Bashir", "kamran.hvac@gmail.com", "03335551234", "Satellite Town, Commercial Area", "Rawalpindi", "AC Repair, Appliance Repair", "HVAC Specialist Certification", 5, 700m, true, 68),
                ("Naveed Akhtar", "naveed.carpenter@gmail.com", "03456789012", "Gulshan-e-Iqbal, Block 4", "Karachi", "Carpentry, General Repairs", "Professional Woodworking Diploma", 10, 500m, true, 60),
                ("Waqas Ahmed", "waqas.painter@gmail.com", "03123456789", "DHA Phase 5, Sector C", "Lahore", "Painting, General Repairs", "Interior & Exterior Painting Certificate", 6, 450m, true, 52),
                ("Shahid Raza", "shahid.appliance@gmail.com", "03028765432", "Madina Town, Main Bazar", "Faisalabad", "Appliance Repair, Electrical", "Electronics & Home Appliances Diploma", 8, 550m, true, 45),
                ("Imran Farooq", "imran.sanitary@gmail.com", "03051239876", "Cantt Area, Mall Road", "Multan", "Sanitary, Plumbing", "Vocational Training in Sanitary Systems", 4, 400m, true, 35),
                ("Bilal Haider", "bilal.ac.tech@gmail.com", "03347890123", "G.T Road, Model Town", "Gujranwala", "AC Repair, Electrical", "Certified AC & Inverter Technician", 3, 500m, false, 8),
                ("Rashid Minhas", "rashid.handyman@gmail.com", "03159081234", "Defence Road, S.I.T.E", "Sialkot", "General Repairs, Carpentry, Painting", "Associate Degree in Civil Technology", 5, 450m, false, 4),
                ("Hamza Shabbir", "hamza.electrician@gmail.com", "03076543210", "Johar Town, Phase 2", "Lahore", "Electrical, Appliance Repair", "Certified Solar & Electrical Technician", 4, 500m, true, 18)
            };

            var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("Provider@123");

            foreach (var sp in dummySPs)
            {
                if (!await context.Users.AnyAsync(u => u.Email == sp.Email))
                {
                    var user = new User
                    {
                        FullName = sp.Name,
                        Email = sp.Email,
                        PasswordHash = defaultPasswordHash,
                        PhoneNumber = sp.Phone,
                        Address = sp.Address,
                        UserType = UserType.ServiceProvider,
                        RegDate = DateTime.UtcNow.AddDays(-sp.DaysAgo),
                        IsActive = true,
                        IsEmailVerified = true,
                        ServiceProvider = new ServiceProvider
                        {
                            WorkExperience = sp.Exp,
                            Expertise = sp.Expertise,
                            Qualification = sp.Qualification,
                            HourlyRate = sp.Rate,
                            IsApproved = sp.Approved,
                            AvgRating = 0.0m,
                            TotalBookings = 0,
                            Location = sp.Location
                        }
                    };

                    context.Users.Add(user);
                }
            }

            await context.SaveChangesAsync();
        }

        // Ensure Bookings table exists even if EF migration hasn't been manually applied yet
        public static async Task EnsureBookingsTableAsync(ApplicationDbContext context)
        {
            const string sql = @"
                IF OBJECT_ID(N'[dbo].[Bookings]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Bookings](
                        [Id] [int] IDENTITY(1,1) NOT NULL,
                        [BookingCode] [nvarchar](30) NOT NULL,
                        [CustomerId] [int] NOT NULL,
                        [CustomerName] [nvarchar](150) NOT NULL,
                        [CustomerPhone] [nvarchar](30) NULL,
                        [ProviderId] [int] NOT NULL,
                        [ProviderName] [nvarchar](150) NOT NULL,
                        [ServiceType] [nvarchar](150) NOT NULL,
                        [ScheduledWhen] [nvarchar](50) NOT NULL,
                        [Price] [decimal](10, 2) NOT NULL DEFAULT(0.00),
                        [Status] [nvarchar](30) NOT NULL DEFAULT(N'Pending'),
                        [Address] [nvarchar](500) NOT NULL,
                        [Instructions] [nvarchar](1000) NULL,
                        [PaymentStatus] [nvarchar](30) NOT NULL DEFAULT(N'Pending'),
                        [PaymentMethod] [nvarchar](50) NULL,
                        [CancelReason] [nvarchar](500) NULL,
                        [ProposedWhen] [nvarchar](50) NULL,
                        [ProposalNote] [nvarchar](500) NULL,
                        [ProposalStatus] [nvarchar](40) NULL,
                        [CreatedAt] [datetime2](7) NOT NULL DEFAULT(GETUTCDATE()),
                        CONSTRAINT [PK_Bookings] PRIMARY KEY CLUSTERED ([Id] ASC),
                        CONSTRAINT [FK_Bookings_Customers_CustomerId] FOREIGN KEY([CustomerId]) REFERENCES [dbo].[Customers]([CustomerId]),
                        CONSTRAINT [FK_Bookings_Service_Providers_ProviderId] FOREIGN KEY([ProviderId]) REFERENCES [dbo].[Service_Providers]([ProviderId])
                    );
                    CREATE UNIQUE NONCLUSTERED INDEX [IX_Bookings_BookingCode] ON [dbo].[Bookings]([BookingCode] ASC);
                END
                IF COL_LENGTH(N'[dbo].[Bookings]', N'ProposedWhen') IS NULL
                    ALTER TABLE [dbo].[Bookings] ADD [ProposedWhen] [nvarchar](50) NULL;
                IF COL_LENGTH(N'[dbo].[Bookings]', N'ProposalNote') IS NULL
                    ALTER TABLE [dbo].[Bookings] ADD [ProposalNote] [nvarchar](500) NULL;
                IF COL_LENGTH(N'[dbo].[Bookings]', N'ProposalStatus') IS NULL
                    ALTER TABLE [dbo].[Bookings] ADD [ProposalStatus] [nvarchar](40) NULL;";
            await context.Database.ExecuteSqlRawAsync(sql);
        }

        // Ensure Reviews table exists cleanly
        public static async Task EnsureReviewsTableAsync(ApplicationDbContext context)
        {
            const string sql = @"
                IF OBJECT_ID(N'[dbo].[Reviews]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Reviews](
                        [Id] [int] IDENTITY(1,1) NOT NULL,
                        [ReviewCode] [nvarchar](30) NOT NULL,
                        [BookingCode] [nvarchar](30) NULL,
                        [CustomerId] [int] NOT NULL,
                        [CustomerName] [nvarchar](150) NOT NULL,
                        [ProviderId] [int] NOT NULL,
                        [ProviderName] [nvarchar](150) NOT NULL,
                        [Stars] [int] NOT NULL,
                        [Comment] [nvarchar](1000) NOT NULL,
                        [Status] [nvarchar](30) NOT NULL DEFAULT(N'Published'),
                        [RemovedAt] [nvarchar](60) NULL,
                        [Reply] [nvarchar](1000) NULL,
                        [ReplyAt] [datetime2](7) NULL,
                        [UpdatedAt] [datetime2](7) NULL,
                        [CreatedAt] [datetime2](7) NOT NULL DEFAULT(GETUTCDATE()),
                        CONSTRAINT [PK_Reviews] PRIMARY KEY CLUSTERED ([Id] ASC),
                        CONSTRAINT [FK_Reviews_Customers_CustomerId] FOREIGN KEY([CustomerId]) REFERENCES [dbo].[Customers]([CustomerId]),
                        CONSTRAINT [FK_Reviews_Service_Providers_ProviderId] FOREIGN KEY([ProviderId]) REFERENCES [dbo].[Service_Providers]([ProviderId])
                    );
                    CREATE UNIQUE NONCLUSTERED INDEX [IX_Reviews_ReviewCode] ON [dbo].[Reviews]([ReviewCode] ASC);
                END";
            await context.Database.ExecuteSqlRawAsync(sql);
        }

        public static async Task EnsureMissingSchemaAsync(ApplicationDbContext context)
        {
            const string sql = @"
                -- Users lockout & temp deactivation
                IF COL_LENGTH(N'[dbo].[Users]', N'FailedLoginAttempts') IS NULL
                    ALTER TABLE [dbo].[Users] ADD [FailedLoginAttempts] [int] NOT NULL DEFAULT(0);
                IF COL_LENGTH(N'[dbo].[Users]', N'LockoutEnd') IS NULL
                    ALTER TABLE [dbo].[Users] ADD [LockoutEnd] [datetime2](7) NULL;
                IF COL_LENGTH(N'[dbo].[Users]', N'IsTemporarilyDeactivated') IS NULL
                    ALTER TABLE [dbo].[Users] ADD [IsTemporarilyDeactivated] [bit] NOT NULL DEFAULT(0);

                -- Service_Providers availability & rejection reason
                IF COL_LENGTH(N'[dbo].[Service_Providers]', N'IsAvailable') IS NULL
                    ALTER TABLE [dbo].[Service_Providers] ADD [IsAvailable] [bit] NOT NULL DEFAULT(1);
                IF COL_LENGTH(N'[dbo].[Service_Providers]', N'RejectionReason') IS NULL
                    ALTER TABLE [dbo].[Service_Providers] ADD [RejectionReason] [nvarchar](500) NULL;

                -- Customers saved addresses & searches
                IF COL_LENGTH(N'[dbo].[Customers]', N'SavedAddressesJson') IS NULL
                    ALTER TABLE [dbo].[Customers] ADD [SavedAddressesJson] [nvarchar](max) NULL;
                IF COL_LENGTH(N'[dbo].[Customers]', N'SavedSearchesJson') IS NULL
                    ALTER TABLE [dbo].[Customers] ADD [SavedSearchesJson] [nvarchar](max) NULL;

                -- Bookings reminder & payment tracking columns
                IF COL_LENGTH(N'[dbo].[Bookings]', N'ReminderSent') IS NULL
                    ALTER TABLE [dbo].[Bookings] ADD [ReminderSent] [bit] NOT NULL DEFAULT(0);
                IF COL_LENGTH(N'[dbo].[Bookings]', N'PaymentChannel') IS NULL
                    ALTER TABLE [dbo].[Bookings] ADD [PaymentChannel] [nvarchar](50) NULL;
                IF COL_LENGTH(N'[dbo].[Bookings]', N'PaymentReference') IS NULL
                    ALTER TABLE [dbo].[Bookings] ADD [PaymentReference] [nvarchar](100) NULL;
                IF COL_LENGTH(N'[dbo].[Bookings]', N'PaymentProofUrl') IS NULL
                    ALTER TABLE [dbo].[Bookings] ADD [PaymentProofUrl] [nvarchar](500) NULL;
                IF COL_LENGTH(N'[dbo].[Bookings]', N'PaymentNote') IS NULL
                    ALTER TABLE [dbo].[Bookings] ADD [PaymentNote] [nvarchar](500) NULL;
                IF COL_LENGTH(N'[dbo].[Bookings]', N'PaymentSubmittedAt') IS NULL
                    ALTER TABLE [dbo].[Bookings] ADD [PaymentSubmittedAt] [datetime2](7) NULL;
                IF COL_LENGTH(N'[dbo].[Bookings]', N'PaymentVerifiedAt') IS NULL
                    ALTER TABLE [dbo].[Bookings] ADD [PaymentVerifiedAt] [datetime2](7) NULL;

                -- Reviews reply & updated timestamp
                IF COL_LENGTH(N'[dbo].[Reviews]', N'Reply') IS NULL
                    ALTER TABLE [dbo].[Reviews] ADD [Reply] [nvarchar](1000) NULL;
                IF COL_LENGTH(N'[dbo].[Reviews]', N'ReplyAt') IS NULL
                    ALTER TABLE [dbo].[Reviews] ADD [ReplyAt] [datetime2](7) NULL;
                IF COL_LENGTH(N'[dbo].[Reviews]', N'UpdatedAt') IS NULL
                    ALTER TABLE [dbo].[Reviews] ADD [UpdatedAt] [datetime2](7) NULL;

                -- Complaints table
                IF OBJECT_ID(N'[dbo].[Complaints]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Complaints](
                        [Id] [int] IDENTITY(1,1) NOT NULL,
                        [ComplaintCode] [nvarchar](30) NOT NULL,
                        [BookingCode] [nvarchar](30) NULL,
                        [CustomerId] [int] NOT NULL,
                        [CustomerName] [nvarchar](150) NOT NULL,
                        [ProviderId] [int] NOT NULL,
                        [ProviderName] [nvarchar](150) NOT NULL,
                        [Subject] [nvarchar](200) NOT NULL,
                        [Description] [nvarchar](2000) NOT NULL,
                        [Status] [nvarchar](30) NOT NULL DEFAULT(N'Pending'),
                        [Severity] [nvarchar](30) NOT NULL DEFAULT(N'Normal'),
                        [EvidenceUrl] [nvarchar](500) NULL,
                        [AdminNotes] [nvarchar](1000) NULL,
                        [DismissReason] [nvarchar](500) NULL,
                        [WarningsIssued] [int] NOT NULL DEFAULT(0),
                        [FiledAt] [nvarchar](50) NOT NULL,
                        [ResolvedAt] [datetime2](7) NULL,
                        [CreatedAt] [datetime2](7) NOT NULL DEFAULT(GETUTCDATE()),
                        CONSTRAINT [PK_Complaints] PRIMARY KEY CLUSTERED ([Id] ASC)
                    );
                    CREATE UNIQUE NONCLUSTERED INDEX [IX_Complaints_ComplaintCode] ON [dbo].[Complaints]([ComplaintCode] ASC);
                END

                -- BookingIssues table (SP Reports Issue)
                IF OBJECT_ID(N'[dbo].[BookingIssues]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[BookingIssues](
                        [Id] [int] IDENTITY(1,1) NOT NULL,
                        [IssueCode] [nvarchar](30) NOT NULL,
                        [BookingCode] [nvarchar](30) NOT NULL,
                        [ProviderId] [int] NOT NULL,
                        [ProviderName] [nvarchar](150) NOT NULL,
                        [CustomerId] [int] NOT NULL,
                        [CustomerName] [nvarchar](150) NOT NULL,
                        [IssueType] [nvarchar](100) NOT NULL,
                        [Description] [nvarchar](1000) NOT NULL,
                        [Status] [nvarchar](30) NOT NULL DEFAULT(N'Open'),
                        [AdminNotes] [nvarchar](1000) NULL,
                        [CreatedAt] [datetime2](7) NOT NULL DEFAULT(GETUTCDATE()),
                        CONSTRAINT [PK_BookingIssues] PRIMARY KEY CLUSTERED ([Id] ASC)
                    );
                END

                -- CustomerFavorites table
                IF OBJECT_ID(N'[dbo].[CustomerFavorites]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[CustomerFavorites](
                        [Id] [int] IDENTITY(1,1) NOT NULL,
                        [CustomerId] [int] NOT NULL,
                        [ProviderId] [int] NOT NULL,
                        [CreatedAt] [datetime2](7) NOT NULL DEFAULT(GETUTCDATE()),
                        CONSTRAINT [PK_CustomerFavorites] PRIMARY KEY CLUSTERED ([Id] ASC),
                        CONSTRAINT [UQ_CustomerFavorites_Pair] UNIQUE ([CustomerId], [ProviderId])
                    );
                END

                -- Notifications table
                IF OBJECT_ID(N'[dbo].[Notifications]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Notifications](
                        [Id] [int] IDENTITY(1,1) NOT NULL,
                        [UserId] [int] NOT NULL,
                        [Title] [nvarchar](200) NOT NULL,
                        [Message] [nvarchar](1000) NOT NULL,
                        [Type] [nvarchar](30) NOT NULL DEFAULT(N'info'),
                        [LinkUrl] [nvarchar](250) NULL,
                        [IsRead] [bit] NOT NULL DEFAULT(0),
                        [CreatedAt] [datetime2](7) NOT NULL DEFAULT(GETUTCDATE()),
                        CONSTRAINT [PK_Notifications] PRIMARY KEY CLUSTERED ([Id] ASC)
                    );
                    CREATE INDEX [IX_Notifications_UserId_CreatedAt] ON [dbo].[Notifications]([UserId] ASC, [CreatedAt] DESC);
                END";
            await context.Database.ExecuteSqlRawAsync(sql);
        }
    }
}
