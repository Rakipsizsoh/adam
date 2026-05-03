using MyIRC.Domain.Entities.Users;

namespace MyIRC.Application.Interfaces.Repositories
{
    public interface IUserAccountRepository
    {
        Task<bool> ExistsByNickAsync(string nick);

        Task<UserAccount?> GetByNickAsync(string nick);

        Task AddAsync(UserAccount user);
    }
}