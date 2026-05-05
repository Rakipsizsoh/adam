using MyIRC.Application.Interfaces.Repositories;
using MyIRC.Application.Interfaces.Security;
using MyIRC.Domain.Entities.Channels;

namespace MyIRC.Application.Services.ChanServ
{
    public class ChanServService
    {
        private readonly IChannelRegistrationRepository _repository;
        private readonly IPasswordHasher _hasher;

        public ChanServService(
            IChannelRegistrationRepository repository,
            IPasswordHasher hasher)
        {
            _repository = repository;
            _hasher = hasher;
        }

        public async Task<(bool Success, string Message)> RegisterAsync(
            string channelName,
            string password,
            string description,
            int userId,
            string nick)
        {
            if (!channelName.StartsWith("#"))
            {
                return (false, "Geçerli bir kanal adı girin. (# ile başlamalı)");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return (false, "Kanal şifresi boş olamaz.");
            }

            var exists = await _repository.ExistsByChannelAsync(channelName);

            if (exists)
            {
                return (false, "Bu kanal zaten kayıtlı.");
            }

            var hashed = _hasher.HashPassword(password);

            var channel = new ChannelRegistration
            {
                ChannelName = channelName,
                PasswordHash = hashed,
                Description = description,
                FounderUserAccountId = userId,
                FounderNick = nick,
                Modes = "+r"
            };

            await _repository.AddAsync(channel);

            return (true, $"{channelName} kanalı başarıyla kayıt edildi.");
        }

        public async Task<(bool Success, string Message, ChannelRegistration? Channel)> IdentifyAsync(
            string channelName,
            string password)
        {
            if (!channelName.StartsWith("#"))
            {
                return (false, "Geçersiz kanal adı.", null);
            }

            var channel = await _repository.GetByChannelAsync(channelName);

            if (channel == null)
            {
                return (false, "Bu kanal kayıtlı değil.", null);
            }

            var valid = _hasher.VerifyPassword(password, channel.PasswordHash);

            if (!valid)
            {
                return (false, "Şifre hatalı.", null);
            }

            return (true, "Kanal doğrulandı.", channel);
        }
    }
}
