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

        // channelName -> nick listesi
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _channelInvites = new();

        // 🔥 channelName -> key
        private readonly ConcurrentDictionary<string, string> _channelKeys = new();

        // 🔥 channelName -> limit
        private readonly ConcurrentDictionary<string, int> _channelLimits = new();

        private readonly ConcurrentDictionary<string, string> _channelModes = new();

        // 🔥 channelName -> topic
        private readonly ConcurrentDictionary<string, string> _channelTopics = new();
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

        public bool IsUserInvited(string channelName, string nick)
        {
            if (string.IsNullOrWhiteSpace(channelName) || string.IsNullOrWhiteSpace(nick))
                return false;

            if (!_channelInvites.TryGetValue(channelName, out var invitedUsers))
                return false;

            return invitedUsers.ContainsKey(NormalizeNick(nick));
        }

        public void AddInvite(string channelName, string nick)
        {
            if (string.IsNullOrWhiteSpace(channelName) || string.IsNullOrWhiteSpace(nick))
                return;

            var invitedUsers = _channelInvites.GetOrAdd(
                channelName,
                _ => new ConcurrentDictionary<string, byte>()
            );

            invitedUsers.TryAdd(NormalizeNick(nick), 0);
        }

        public void RemoveInvite(string channelName, string nick)
        {
            if (string.IsNullOrWhiteSpace(channelName) || string.IsNullOrWhiteSpace(nick))
                return;

            if (!_channelInvites.TryGetValue(channelName, out var invitedUsers))
                return;

            invitedUsers.TryRemove(NormalizeNick(nick), out _);

            if (invitedUsers.IsEmpty)
                _channelInvites.TryRemove(channelName, out _);
        }

        public string GetChannelModes(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                return "+";

            if (_channelModes.TryGetValue(channelName, out var modes))
                return modes;

            return "+";
        }

        public void SetChannelModes(string channelName, string modes)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                return;

            _channelModes[channelName] = modes;
        }

        public void RemoveChannelModes(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                return;

            _channelModes.TryRemove(channelName, out _);
        }

        private static string NormalizeNick(string nick)
        {
            return nick.Trim().ToLowerInvariant();
        }


        // +k için
        public string? GetChannelKey(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                return null;

            return _channelKeys.TryGetValue(channelName, out var key)
                ? key
                : null;
        }

        public void SetChannelKey(string channelName, string key)
        {
            if (string.IsNullOrWhiteSpace(channelName) || string.IsNullOrWhiteSpace(key))
                return;

            _channelKeys[channelName] = key;
        }

        public void RemoveChannelKey(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                return;

            _channelKeys.TryRemove(channelName, out _);
        }

        // +t için

        public string? GetChannelTopic(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                return null;

            return _channelTopics.TryGetValue(channelName, out var topic)
                ? topic
                : null;
        }

        public void SetChannelTopic(string channelName, string topic)
        {
            if (string.IsNullOrWhiteSpace(channelName) || string.IsNullOrWhiteSpace(topic))
                return;

            _channelTopics[channelName] = topic;
        }

        // +l için

        public int? GetChannelLimit(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                return null;

            return _channelLimits.TryGetValue(channelName, out var limit)
                ? limit
                : null;
        }

        public void SetChannelLimit(string channelName, int limit)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                return;

            _channelLimits[channelName] = limit;
        }

        public void RemoveChannelLimit(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                return;

            _channelLimits.TryRemove(channelName, out _);
        }
    }
}
