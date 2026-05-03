using System.Collections.Concurrent;
using MyIRC.Application.Interfaces.Stores;
using MyIRC.Domain.Entities.Irc;

namespace MyIRC.Infrastructure.Stores
{
    public class OnlineUserStore : IOnlineUserStore
    {
        private readonly ConcurrentDictionary<string, OnlineUser> _users = new();

        // nick(lower) -> connectionId
        private readonly ConcurrentDictionary<string, string> _nickIndex = new();

        // channelName -> connectionId listesi
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _channelUsers = new();

        public IReadOnlyCollection<OnlineUser> GetAll()
        {
            return _users.Values.ToList();
        }

        public OnlineUser? GetByConnectionId(string connectionId)
        {
            _users.TryGetValue(connectionId, out var user);
            return user;
        }

        public OnlineUser? GetByNick(string nick)
        {
            var key = NormalizeNick(nick);

            if (!_nickIndex.TryGetValue(key, out var connectionId))
                return null;

            return GetByConnectionId(connectionId);
        }

        public bool Add(OnlineUser user)
        {
            var nickKey = NormalizeNick(user.Nick);

            if (!_users.TryAdd(user.ConnectionId, user))
                return false;

            if (!_nickIndex.TryAdd(nickKey, user.ConnectionId))
            {
                _users.TryRemove(user.ConnectionId, out _);
                return false;
            }

            return true;
        }

        public bool Remove(string connectionId, out OnlineUser? user)
        {
            var removed = _users.TryRemove(connectionId, out user);

            if (removed && user != null)
            {
                _nickIndex.TryRemove(NormalizeNick(user.Nick), out _);

                foreach (var channelName in user.Channels.ToList())
                {
                    RemoveUserFromChannelIndex(connectionId, channelName);
                }
            }

            return removed;
        }

        public bool NickExists(string nick)
        {
            return _nickIndex.ContainsKey(NormalizeNick(nick));
        }

        public IReadOnlyCollection<OnlineUser> GetUsersInChannel(string channelName)
        {
            if (!_channelUsers.TryGetValue(channelName, out var connections))
                return [];

            var result = new List<OnlineUser>();

            foreach (var connectionId in connections.Keys)
            {
                if (_users.TryGetValue(connectionId, out var user))
                    result.Add(user);
            }

            return result;
        }

        public void AddUserToChannelIndex(string connectionId, string channelName)
        {
            var channel = _channelUsers.GetOrAdd(
                channelName,
                _ => new ConcurrentDictionary<string, byte>()
            );

            channel.TryAdd(connectionId, 0);
        }

        public void RemoveUserFromChannelIndex(string connectionId, string channelName)
        {
            if (!_channelUsers.TryGetValue(channelName, out var channel))
                return;

            channel.TryRemove(connectionId, out _);

            if (channel.IsEmpty)
                _channelUsers.TryRemove(channelName, out _);
        }

        public bool UpdateNickIndex(string connectionId, string oldNick, string newNick)
        {
            var oldKey = NormalizeNick(oldNick);
            var newKey = NormalizeNick(newNick);

            if (_nickIndex.ContainsKey(newKey))
                return false;

            _nickIndex.TryRemove(oldKey, out _);

            return _nickIndex.TryAdd(newKey, connectionId);
        }

        private static string NormalizeNick(string nick)
        {
            return nick.Trim().ToLowerInvariant();
        }
    }
}