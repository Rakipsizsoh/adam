using Microsoft.EntityFrameworkCore;
using MyIRC.Domain.Entities.Users;
using MyIRC.Domain.Entities.Channels;

namespace MyIRC.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<UserAccount> Users => Set<UserAccount>();

        public DbSet<ChannelRegistration> ChannelRegistrations => Set<ChannelRegistration>();
    }
}