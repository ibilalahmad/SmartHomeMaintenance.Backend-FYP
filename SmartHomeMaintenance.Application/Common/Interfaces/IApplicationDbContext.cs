using Microsoft.EntityFrameworkCore;
using SmartHomeMaintenance.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartHomeMaintenance.Application.Common.Interfaces
{
    public interface IApplicationDbContext
    {
        DbSet<User> Users { get; }
        DbSet<Customer> Customers { get; }
        DbSet<ServiceProvider> ServiceProviders { get; }
        DbSet<Admin> Admins { get; }
        DbSet<Booking> Bookings { get; }
        DbSet<Review> Reviews { get; }
        DbSet<Complaint> Complaints { get; }
        DbSet<BookingIssue> BookingIssues { get; }
        DbSet<CustomerFavorite> CustomerFavorites { get; }
        DbSet<AppNotification> Notifications { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
