using MyIRC.Application.Interfaces.Repositories;
using MyIRC.Application.Interfaces.Security;
using MyIRC.Domain.Entities.Users;

namespace MyIRC.Application.Services.NickServ
{
    public class NickServService
    {
        private readonly IUserAccountRepository _repository;
        private readonly IPasswordHasher _hasher;

        public NickServService(
            IUserAccountRepository repository,
            IPasswordHasher hasher)
        {
            _repository = repository;
            _hasher = hasher;
        }

        public async Task<(bool Success, string Message)> RegisterAsync(
            string currentNick,
            string password,
            string email)
        {
            if (await _repository.ExistsByNickAsync(currentNick))
            {
                return (false, "Bu nick zaten kayıtlı.");
            }

            var hashed = _hasher.HashPassword(password);

            var user = new UserAccount
            {
                Nick = currentNick,
                Email = email,
                PasswordHash = hashed
            };

            await _repository.AddAsync(user);

            return (true, "Nick başarıyla kayıt edildi.");
        }

        public async Task<(bool Success, string Message, UserAccount? User)> IdentifyAsync(
            string currentNick,
            string password)
        {
            var user = await _repository.GetByNickAsync(currentNick);

            if (user == null)
            {
                return (false, "Bu nick kayıtlı değil.", null);
            }

            var valid = _hasher.VerifyPassword(password, user.PasswordHash);

            if (!valid)
            {
                return (false, "Şifre yanlış.", null);
            }

            return (true, "Giriş başarılı.", user);
        }

        public async Task<bool> IsNickRegisteredAsync(string nick)
        {
            return await _repository.ExistsByNickAsync(nick);
        }
    }
}