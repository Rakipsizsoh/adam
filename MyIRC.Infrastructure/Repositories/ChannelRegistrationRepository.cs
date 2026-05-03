using Microsoft.EntityFrameworkCore;
using MyIRC.Application.Interfaces.Repositories;
using MyIRC.Domain.Entities.Channels;
using MyIRC.Infrastructure.Data;

namespace MyIRC.Infrastructure.Repositories
{
    public class ChannelRegistrationRepository : IChannelRegistrationRepository
    {
        private readonly AppDbContext _context;

        public ChannelRegistrationRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> ExistsByChannelAsync(string channelName)
        {
            return await _context.ChannelRegistrations
                .AnyAsync(x => x.ChannelName == channelName && x.IsActive);
        }

        public async Task<ChannelRegistration?> GetByChannelAsync(string channelName)
        {
            return await _context.ChannelRegistrations
                .FirstOrDefaultAsync(x => x.ChannelName == channelName && x.IsActive);
        }

        public async Task AddAsync(ChannelRegistration registration)
        {
            _context.ChannelRegistrations.Add(registration);
            await _context.SaveChangesAsync();
        }

        // 🔥 MODE için gerekli UPDATE
        public async Task UpdateAsync(ChannelRegistration registration)
        {
            _context.ChannelRegistrations.Update(registration);
            await _context.SaveChangesAsync();
        }

        // 🔥 AUTO JOIN

        public async Task<List<string>> GetDefaultJoinChannelsAsync()
        {
            return await _context.ChannelRegistrations
                .Where(x => x.IsActive && x.IsDefaultJoin)
                .OrderBy(x => x.Id)
                .Select(x => x.ChannelName)
                .ToListAsync();
        }
    }
}