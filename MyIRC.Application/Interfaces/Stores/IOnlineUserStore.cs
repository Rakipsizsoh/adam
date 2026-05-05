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

        // 🔥 INVITE ONLY (+i)
        bool IsUserInvited(string channelName, string nick);

        void AddInvite(string channelName, string nick);

        void RemoveInvite(string channelName, string nick);

        // 🔥 RUNTIME CHANNEL MODES
        string GetChannelModes(string channelName);

        void SetChannelModes(string channelName, string modes);

        void RemoveChannelModes(string channelName);

        // 🔥 RUNTIME CHANNEL KEYS (+k)
        string? GetChannelKey(string channelName);

        void SetChannelKey(string channelName, string key);

        void RemoveChannelKey(string channelName);

        // 🔥 RUNTIME CHANNEL LIMITS (+l)
        int? GetChannelLimit(string channelName);

        void SetChannelLimit(string channelName, int limit);

        void RemoveChannelLimit(string channelName);

        // 🔥 RUNTIME CHANNEL TOPICS
        string? GetChannelTopic(string channelName);

        void SetChannelTopic(string channelName, string topic);
    }
}
