using MyIRC.Domain.Entities.Irc;

namespace MyIRC.Application.Interfaces.Stores
{
    public interface IOnlineUserStore
    {
        IReadOnlyCollection<OnlineUser> GetAll();

        OnlineUser? GetByConnectionId(string connectionId);

        OnlineUser? GetByNick(string nick);

        bool Add(OnlineUser user);

        bool Remove(string connectionId, out OnlineUser? user);

        bool NickExists(string nick);

        IReadOnlyCollection<OnlineUser> GetUsersInChannel(string channelName);

        void AddUserToChannelIndex(string connectionId, string channelName);

        void RemoveUserFromChannelIndex(string connectionId, string channelName);

        bool UpdateNickIndex(string connectionId, string oldNick, string newNick);
    }
}