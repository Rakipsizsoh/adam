using Microsoft.EntityFrameworkCore;
using MyIRC.Application.Interfaces.Repositories;
using MyIRC.Domain.Entities.Users;
using MyIRC.Infrastructure.Data;

namespace MyIRC.Infrastructure.Repositories
{
    public class UserAccountRepository : IUserAccountRepository
    {
        private readonly AppDbContext _context;

        public UserAccountRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> ExistsByNickAsync(string nick)
        {
            return await _context.Users
                .AnyAsync(x => x.Nick.ToLower() == nick.ToLower());
        }

        public async Task<UserAccount?> GetByNickAsync(string nick)
        {
            return await _context.Users
                .FirstOrDefaultAsync(x => x.Nick.ToLower() == nick.ToLower());
        }

        public async Task AddAsync(UserAccount user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }
    }
}