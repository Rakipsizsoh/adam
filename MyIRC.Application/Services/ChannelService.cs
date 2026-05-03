using MyIRC.Domain.Entities.Irc;
using MyIRC.Domain.Enums;
using MyIRC.Application.Interfaces.Stores;

namespace MyIRC.Application.Services
{
    public class ChannelService
    {
        private readonly IOnlineUserStore _store;

        public ChannelService(IOnlineUserStore store)
        {
            _store = store;
        }

        public bool Join(string connectionId, string channelName, out OnlineUser? user)
        {
            user = _store.GetByConnectionId(connectionId);

            if (user == null)
                return false;

            if (!channelName.StartsWith("#"))
                channelName = "#" + channelName;

            if (user.Channels.Contains(channelName))
                return false;

            user.Channels.Add(channelName);
            _store.AddUserToChannelIndex(connectionId, channelName);

            user.CurrentChannel = channelName;

            if (!user.ChannelRoles.ContainsKey(channelName))
            {
                var channelUserCount = _store.GetUsersInChannel(channelName).Count;

                user.ChannelRoles[channelName] =
                    channelUserCount == 1
                        ? ChannelRole.Op
                        : ChannelRole.User;
            }

            return true;
        }

        public bool Part(string connectionId, string channelName, out OnlineUser? user)
        {
            user = _store.GetByConnectionId(connectionId);

            if (user == null)
                return false;

            if (!channelName.StartsWith("#"))
                channelName = "#" + channelName;

            if (!user.Channels.Contains(channelName))
                return false;

            if (user.Channels.Count <= 1)
                return false;

            user.Channels.Remove(channelName);
            _store.RemoveUserFromChannelIndex(connectionId, channelName);

            if (user.ChannelRoles.ContainsKey(channelName))
                user.ChannelRoles.Remove(channelName);

            if (user.CurrentChannel == channelName)
            {
                user.CurrentChannel = user.Channels.First();
            }

            return true;
        }

        public List<string> Quit(string connectionId, out OnlineUser? user)
        {
            user = _store.GetByConnectionId(connectionId);

            if (user == null)
                return new List<string>();

            var channels = new List<string>(user.Channels);

            user.ChannelRoles.Clear();

            _store.Remove(connectionId, out _);

            return channels;
        }
        public bool ChangeNick(string connectionId, string newNick, out OnlineUser? user, out string oldNick)
        {
            oldNick = string.Empty;

            user = _store.GetByConnectionId(connectionId);

            if (user == null)
                return false;

            newNick = newNick.Trim();

            if (string.IsNullOrWhiteSpace(newNick))
                return false;

            oldNick = user.Nick;

            if (!_store.UpdateNickIndex(connectionId, oldNick, newNick))
                return false;

            user.Nick = newNick;

            return true;
        }
    }
}