using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyIRC.Domain.Entities.Channels;

namespace MyIRC.Application.Interfaces.Repositories
{
    public interface IChannelRegistrationRepository
    {
        Task<bool> ExistsByChannelAsync(string channelName);

        Task<ChannelRegistration?> GetByChannelAsync(string channelName);

        Task AddAsync(ChannelRegistration registration);

        Task UpdateAsync(ChannelRegistration registration);

        // 🔥 AUTO JOIN
        Task<List<string>> GetDefaultJoinChannelsAsync();
    }
}