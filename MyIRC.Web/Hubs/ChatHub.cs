// ChatHub.cs

using Microsoft.AspNetCore.SignalR;
using MyIRC.Application.Interfaces.Repositories;
using MyIRC.Application.Services;
using MyIRC.Application.Services.ChannelModes;
using MyIRC.Application.Services.ChanServ;
using MyIRC.Application.Services.NickServ;
using MyIRC.Domain.Entities.Irc;
using MyIRC.Domain.Enums;
using MyIRC.Application.Interfaces.Stores;
using System.Threading.Channels;


namespace MyIRC.Web.Hubs
{
    public class ChatHub : Hub
    {   private readonly ChannelService _channelService;
        private readonly NickServService _nickServ;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly MyIRC.Application.Services.ChanServ.ChanServService _chanServ;
        private readonly IChannelRegistrationRepository _channelRegistrationRepository;
        private readonly ChannelModeService _channelModeService;

        // 🔥 YENİ
        private readonly IOnlineUserStore _onlineUserStore;

        public ChatHub(
    NickServService nickServ,
    IHubContext<ChatHub> hubContext,
    ChanServService chanServ,
    IChannelRegistrationRepository channelRegistrationRepository,
    ChannelModeService channelModeService,
    IOnlineUserStore onlineUserStore)
        {
            _channelService = new ChannelService(onlineUserStore);
            _nickServ = nickServ;
            _hubContext = hubContext;
            _chanServ = chanServ;
            _channelRegistrationRepository = channelRegistrationRepository;
            _channelModeService = channelModeService;
            _onlineUserStore = onlineUserStore;
        }

        private string GenerateGuestNick()
        {
            var random = Random.Shared.Next(1000, 9999);
            return $"RSohbet_{random}";
        }

        private string NormalizeChannelName(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                return "#Sohbet";

            channelName = channelName.Trim();

            if (!channelName.StartsWith("#"))
                channelName = "#" + channelName;

            var name = channelName.Substring(1).ToLowerInvariant();

            return "#" + char.ToUpperInvariant(name[0]) + name.Substring(1);
        }
        public async Task SendMessage(string user, string message, string? activeChannel = null)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            if (message.StartsWith("/"))
            {
                var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var command = parts[0].ToLower();

                switch (command)
                {
                    case "/join":
                        if (parts.Length > 1)
                            await JoinChannel(parts[1]);
                        return;

                    case "/part":
                        if (parts.Length > 1)
                            await PartChannel(parts[1]);
                        return;

                    case "/quit":
                        Context.Abort();
                        return;

                    case "/nick":
                        if (parts.Length > 1)
                            await ChangeNick(parts[1]);
                        return;

                    case "/msg":
                        if (parts.Length > 2)
                        {
                            var target = parts[1];
                            var msgText = string.Join(" ", parts.Skip(2));

                            if (target.StartsWith("#"))
                                await SendChannelMessageDirect(target, msgText);
                            else
                                await SendPrivateMessage(target, msgText);
                        }
                        return;

                    case "/me":
                        if (parts.Length > 1)
                            await SendActionMessage(string.Join(" ", parts.Skip(1)), false);
                        return;

                    case "/ame":
                        if (parts.Length > 1)
                            await SendActionMessage(string.Join(" ", parts.Skip(1)), true);
                        return;

                    case "/amsg":
                        if (parts.Length > 1)
                            await SendMessageToAllMyChannels(string.Join(" ", parts.Skip(1)));
                        return;

                    case "/mode":
                        if (parts.Length >= 3)
                        {
                            var targetNick = parts.Length >= 4 ? parts[3] : null;
                            await SetChannelMode(parts[1], parts[2], targetNick);
                        }
                        return;

                    case "/topic":
                        if (parts.Length >= 3)
                            await SetChannelTopic(parts[1], string.Join(" ", parts.Skip(2)));
                        return;

                    case "/ns":
                    case "/nickserv":
                        if (parts.Length >= 3 && parts[1].Equals("identify", StringComparison.OrdinalIgnoreCase))
                            await IdentifyNick(parts[2]);
                        else if (parts.Length >= 4 && parts[1].Equals("register", StringComparison.OrdinalIgnoreCase))
                            await RegisterNick(parts[2], parts[3]);
                        return;

                    case "/cs":
                    case "/chanserv":
                        if (parts.Length >= 4 && parts[1].Equals("register", StringComparison.OrdinalIgnoreCase))
                            await ChanServRegister(parts[2], parts[3], string.Join(" ", parts.Skip(4)));
                        else if (parts.Length >= 4 && parts[1].Equals("identify", StringComparison.OrdinalIgnoreCase))
                            await ChanServIdentify(parts[2], parts[3]);
                        else
                            await Clients.Caller.SendAsync(
                                "ReceiveMessage",
                                "ChanServ",
                                "Kullanım: /cs register #kanal şifre açıklama | /cs identify #kanal şifre",
                                _onlineUserStore.GetByConnectionId(Context.ConnectionId)?.CurrentChannel
                            );
                        return;
                }
            }

            if (message.Length > 500)
                message = message.Substring(0, 500);

            var onlineUser = _onlineUserStore.GetByConnectionId(Context.ConnectionId);
            if (onlineUser == null)
                return;

            var currentChannel = NormalizeChannelName(
                !string.IsNullOrWhiteSpace(activeChannel)
                    ? activeChannel
                    : onlineUser.CurrentChannel
            );

            onlineUser.CurrentChannel = currentChannel;

            var dbChannel = await _channelRegistrationRepository.GetByChannelAsync(currentChannel);

            // 🔥 Kullanıcı bu kanalda değilse normal mesaj gönderemez
            if (!onlineUser.Channels.Contains(currentChannel))
            {
                await Clients.Caller.SendAsync(
                    "ReceiveMessage",
                    "Sistem",
                    $"{currentChannel} kanalında değilsiniz.",
                    onlineUser.CurrentChannel
                );
                return;
            }

            // 🔥 +n
            if (dbChannel != null && dbChannel.Modes.Contains("n"))
            {
                if (!onlineUser.Channels.Contains(currentChannel))
                {
                    await Clients.Caller.SendAsync(
                        "ReceiveMessage",
                        "Sistem",
                        "Bu kanal dışından mesaj kabul etmiyor (+n).",
                        currentChannel
                    );
                    return;
                }
            }

            // 🔥 +M
            if (dbChannel != null && dbChannel.Modes.Contains("M"))
            {
                if (!onlineUser.IsIdentified)
                {
                    await Clients.Caller.SendAsync(
                        "ReceiveMessage",
                        "Sistem",
                        "Bu kanal sadece kayıtlı/identify olmuş kullanıcıların konuşmasına açıktır (+M).",
                        currentChannel
                    );
                    return;
                }
            }

            // 🔥 +m
            if (dbChannel != null && dbChannel.Modes.Contains("m"))
            {
                var role = onlineUser.ChannelRoles.ContainsKey(currentChannel)
                    ? onlineUser.ChannelRoles[currentChannel]
                    : ChannelRole.User;

                if (role < ChannelRole.Voice)
                {
                    await Clients.Caller.SendAsync(
                        "ReceiveMessage",
                        "Sistem",
                        "Bu kanal moderated (+m). Sadece voice ve üzeri konuşabilir.",
                        currentChannel
                    );
                    return;
                }
            }

            await Clients.Group(currentChannel)
                .SendAsync("ReceiveMessage", onlineUser.Nick, message, currentChannel);
        }
        public async Task SendPrivateMessage(string targetNick, string message)
        {
            if (string.IsNullOrWhiteSpace(targetNick) || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (message.Length > 500)
            {
                message = message.Substring(0, 500);
            }

            var sender = _onlineUserStore.GetByConnectionId(Context.ConnectionId);

            if (sender == null)
            {
                return;
            }

            var target = _onlineUserStore.GetByNick(targetNick);

            if (target == null)
            {
                await Clients.Caller.SendAsync("ReceivePrivateMessage", "Sistem", targetNick, $"{targetNick} çevrimdışı.");
                return;
            }

            await Clients.Client(target.ConnectionId)
            .SendAsync("ReceivePrivateMessage", sender.Nick, sender.Nick, message);

            await Clients.Caller
            .SendAsync("ReceivePrivateMessage", sender.Nick, target.Nick, message);
        }
        public Task Ping()
        {
            return Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
        }
        public async Task ChangeNick(string newNick)
        {
            if (string.IsNullOrWhiteSpace(newNick))
                return;

            var currentUser = _onlineUserStore.GetByConnectionId(Context.ConnectionId);

            if (currentUser == null)
                return;

            var isRegisteredNick = await _nickServ.IsNickRegisteredAsync(newNick);

            if (isRegisteredNick && !currentUser.IsIdentified)
            {
                currentUser.IsWaitingForIdentify = true;
                currentUser.IdentifyDeadlineAt = DateTime.UtcNow.AddSeconds(45);
                currentUser.ProtectedNick = newNick;
                currentUser.IsNickProtectedGuest = true;

                var timerId = Guid.NewGuid();
                currentUser.IdentifyTimerId = timerId;

                await Clients.Caller.SendAsync(
                    "ReceiveMessage",
                    "NickServ",
                    $"{newNick} kayıtlı bir nick. Bu nicki almak için 45 saniye içinde /ns identify şifre yazmalısınız.",
                    currentUser.CurrentChannel
                );

                _ = CheckIdentifyTimeoutAsync(currentUser.ConnectionId, timerId);

                return;
            }

            if (!_channelService.ChangeNick(Context.ConnectionId, newNick, out var user, out var oldNick) || user == null)
            {
                await Clients.Caller.SendAsync(
                    "ReceiveMessage",
                    "Sistem",
                    "Nick kullanılıyor.",
                    currentUser.CurrentChannel
                );
                return;
            }

            // 🔥 KENDİ CLIENT
            await Clients.Caller.SendAsync("SetNick", user.Nick);

            foreach (var channel in user.Channels)
            {
                // 🔥 NICK CHANGE EVENT (EN KRİTİK)
                await Clients.Group(channel).SendAsync(
                    "ReceiveNickChanged",
                    oldNick,
                    user.Nick
                );

                // 🔥 MESAJ
                await Clients.Group(channel).SendAsync(
                    "ReceiveMessage",
                    "Sistem",
                    $"{oldNick} nickini {user.Nick} olarak değiştirdi.",
                    channel
                );
            }
        }
        public async Task SendActionMessage(string message, bool allChannels)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            if (message.Length > 500)
                message = message.Substring(0, 500);

            var user = _onlineUserStore.GetByConnectionId(Context.ConnectionId);

            if (user == null)
                return;

            var channels = allChannels
            ? user.Channels
            : new List<string> { user.CurrentChannel };

            foreach (var channel in channels)
            {
                var text = $"* {user.Nick} {message}";

                // 🔥 herkese gönder
                await Clients.Group(channel)
                .SendAsync("ReceiveMessage", "Sistem", text, channel);

                // 🔥 garanti: kendine de gönder
                await Clients.Caller
                .SendAsync("ReceiveMessage", "Sistem", text, channel);
            }
        }
        public async Task SendMessageToAllMyChannels(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            if (message.Length > 500)
                message = message.Substring(0, 500);

            var user = _onlineUserStore.GetByConnectionId(Context.ConnectionId);

            if (user == null)
                return;

            foreach (var channel in user.Channels)
            {
                await Clients.Group(channel)
                .SendAsync("ReceiveMessage", user.Nick, message, channel);
            }
        }
        public async Task RegisterNick(string password, string email)
        {
            var user = _onlineUserStore.GetByConnectionId(Context.ConnectionId);

            if (user == null)
                return;

            var result = await _nickServ.RegisterAsync(user.Nick, password, email);

            await Clients.Caller.SendAsync(
            "ReceiveMessage",
            "NickServ",
            result.Message,
            user.CurrentChannel
            );
        }
        public async Task IdentifyNick(string password)
        {
            var user = _onlineUserStore.GetByConnectionId(Context.ConnectionId);

            if (user == null)
                return;

            var nickToIdentify = user.IsNickProtectedGuest && !string.IsNullOrWhiteSpace(user.ProtectedNick)
                ? user.ProtectedNick
                : user.Nick;

            var result = await _nickServ.IdentifyAsync(nickToIdentify, password);

            if (!result.Success)
            {
                user.IdentifyFailedAttempts++;

                if (user.IdentifyFailedAttempts >= 3)
                {
                    await Clients.Caller.SendAsync(
                        "ReceiveMessage",
                        "NickServ",
                        "3 kez hatalı şifre girdiniz. Sunucudan atılıyorsunuz.",
                        user.CurrentChannel
                    );

                    foreach (var channel in user.Channels)
                    {
                        await Clients.Group(channel).SendAsync(
                            "ReceiveMessage",
                            "Sistem",
                            $"{user.Nick} 3 kez hatalı identify denemesi yaptığı için sunucudan atıldı.",
                            channel
                        );
                    }

                    user.IdentifyTimerId = null;
                    user.IsWaitingForIdentify = false;
                    user.IdentifyDeadlineAt = null;

                    Context.Abort();
                    return;
                }

                await Clients.Caller.SendAsync(
                    "ReceiveMessage",
                    "NickServ",
                    $"{result.Message} Kalan deneme hakkı: {3 - user.IdentifyFailedAttempts}",
                    user.CurrentChannel
                );

                return;
            }

            // ✅ BAŞARILI IDENTIFY
            if (result.User != null)
            {
                user.IsIdentified = true;
                user.UserAccountId = result.User.Id;
                user.IdentifiedNick = result.User.Nick;
                user.IdentifiedAt = DateTime.UtcNow;

                user.IsWaitingForIdentify = false;
                user.IdentifyDeadlineAt = null;
                user.IdentifyTimerId = null;
                user.IdentifyFailedAttempts = 0;

                // 🔥 FOUNDER ROLE CHECK
                foreach (var channel in user.Channels)
                {
                    var registeredChannel = await _channelRegistrationRepository.GetByChannelAsync(channel);

                    if (registeredChannel != null &&
                        registeredChannel.FounderUserAccountId == user.UserAccountId)
                    {
                        user.ChannelRoles[channel] = ChannelRole.Founder;

                        await Clients.Group(channel).SendAsync(
                            "ReceiveUserRoleChanged",
                            channel,
                            user.Nick,
                            (int)ChannelRole.Founder
                        );
                    }
                }

                // 🔥 NICK GERİ ALMA (KRİTİK FIX)
                if (user.IsNickProtectedGuest && !string.IsNullOrWhiteSpace(user.ProtectedNick))
                {
                    var oldNick = user.Nick;
                    var reclaimedNick = user.ProtectedNick;

                    if (!_onlineUserStore.NickExists(reclaimedNick))
                    {
                        // 🔥 INDEX UPDATE (ÇOK KRİTİK)
                        _onlineUserStore.UpdateNickIndex(user.ConnectionId, oldNick, reclaimedNick);

                        user.Nick = reclaimedNick;
                        user.IsNickProtectedGuest = false;
                        user.ProtectedNick = null;

                        // 🔥 KENDİ CLIENT
                        await Clients.Caller.SendAsync("SetNick", user.Nick);

                        foreach (var channel in user.Channels)
                        {
                            // 🔥 EN KRİTİK EVENT
                            await Clients.Group(channel).SendAsync(
                                "ReceiveNickChanged",
                                oldNick,
                                user.Nick
                            );

                            await Clients.Group(channel).SendAsync(
                                "ReceiveMessage",
                                "Sistem",
                                $"{oldNick} nickini {user.Nick} olarak geri aldı.",
                                channel
                            );
                        }
                    }
                }
            }

            await Clients.Caller.SendAsync(
                "ReceiveMessage",
                "NickServ",
                result.Message,
                user.CurrentChannel
            );
        }
        public async Task ChanServRegister(string channelName, string password, string description)
        {
            var user = _onlineUserStore.GetByConnectionId(Context.ConnectionId);

            if (user == null || !user.IsIdentified || user.UserAccountId == null)
            {
                await Clients.Caller.SendAsync(
                    "ReceiveMessage",
                    "ChanServ",
                    "Bu işlemi yapmak için önce identify olmalısınız.",
                    user?.CurrentChannel
                );
                return;
            }

            var result = await _chanServ.RegisterAsync(
                channelName,
                password,
                description,
                user.UserAccountId.Value,
                user.Nick
            );

            await Clients.Caller.SendAsync(
                "ReceiveMessage",
                "ChanServ",
                result.Message,
                user.CurrentChannel
            );

            if (result.Success)
            {
                user.ChannelRoles[channelName] = ChannelRole.Founder;

                await Clients.Group(channelName).SendAsync(
                    "ReceiveMessage",
                    "ChanServ",
                    $"{user.Nick} {channelName} kanalının founder'ı oldu.",
                    channelName
                );
            }
        }
        public async Task ChanServIdentify(string channelName, string password)
        {
            var user = _onlineUserStore.GetByConnectionId(Context.ConnectionId);

            if (user == null)
                return;

            channelName = NormalizeChannelName(channelName);

            var result = await _chanServ.IdentifyAsync(channelName, password);

            if (!result.Success || result.Channel == null)
            {
                await Clients.Caller.SendAsync(
                    "ReceiveMessage",
                    "ChanServ",
                    result.Message,
                    channelName
                );
                return;
            }

            var channel = result.Channel;

            var isFounder =
                user.UserAccountId != null &&
                channel.FounderUserAccountId == user.UserAccountId;

            if (isFounder)
            {
                user.ChannelRoles[channelName] = ChannelRole.Founder;
            }
            else
            {
                user.ChannelRoles[channelName] = ChannelRole.Op;
            }

            await Clients.Caller.SendAsync(
                "ReceiveMessage",
                "ChanServ",
                $"{channelName} için yetkilendirildiniz.",
                channelName
            );

            var roleText = isFounder ? "founder oldu" : "operator oldu";

            await Clients.Group(channelName).SendAsync(
                "ReceiveMessage",
                "Sistem",
                $"{user.Nick} {channelName} kanalının {roleText}.",
                channelName
            );

        }
        private async Task SendChannelTopicInfo(string channelName)
        {
            channelName = NormalizeChannelName(channelName);

            var userCount = _onlineUserStore.GetUsersInChannel(channelName).Count;

            var registeredChannel = await _channelRegistrationRepository.GetByChannelAsync(channelName);

            var modes = string.IsNullOrWhiteSpace(registeredChannel?.Modes)
    ? (registeredChannel != null ? "+r" : "+")
    : registeredChannel.Modes;

            var topic = registeredChannel?.Topic;

            if (string.IsNullOrWhiteSpace(topic))
            {
                topic = $"{channelName.TrimStart('#')} Kanalına Hoşgeldiniz";
            }

            await Clients.Group(channelName).SendAsync(
                "ReceiveTopicInfo",
                channelName,
                userCount,
                modes,
                topic,
                "https://rsohbet.com"
            );
        }
        private async Task SendUserListToCaller(string channelName)
        {
            var users = _onlineUserStore
                .GetUsersInChannel(channelName)
                .Select(u => new
                {
                    u.Nick,
                    Role = u.ChannelRoles.ContainsKey(channelName)
                        ? (int)u.ChannelRoles[channelName]
                        : (int)ChannelRole.User
                })
                .ToList();

            await Clients.Caller.SendAsync(
                "ReceiveUserList",
                channelName,
                users
            );
        }
        public async Task JoinChannel(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                return;

            channelName = NormalizeChannelName(channelName);

            var existingUser = _onlineUserStore.GetByConnectionId(Context.ConnectionId);

            // 🔥 ZATEN KANALDAYSA SADECE AKTİF KANALI DEĞİŞTİR
            if (existingUser != null && existingUser.Channels.Contains(channelName))
            {
                existingUser.CurrentChannel = channelName;

                await Clients.Caller.SendAsync("SetActiveChannel", channelName);

                await SendChannelTopicInfo(channelName);

                return;
            }

            if (!_channelService.Join(Context.ConnectionId, channelName, out var user) || user == null)
                return;

            // 🔥 /join sonrası backend aktif kanal
            user.CurrentChannel = channelName;

            var registeredChannel = await _channelRegistrationRepository.GetByChannelAsync(channelName);

            if (registeredChannel != null)
            {
                user.ChannelRoles[channelName] =
                    user.IsIdentified &&
                    user.UserAccountId == registeredChannel.FounderUserAccountId
                        ? ChannelRole.Founder
                        : ChannelRole.User;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, channelName);

            await Clients.Caller.SendAsync("ChannelJoined", channelName);

            // 🔥 /join sonrası frontend aktif kanal
            await Clients.Caller.SendAsync("SetActiveChannel", channelName);

            await SendUserListToCaller(channelName);

            await Clients.Group(channelName)
                .SendAsync("ReceiveMessage", "Sistem", $"{user.Nick} → {channelName} kanalına katıldı.", channelName);

            await SendChannelTopicInfo(channelName);
        }
        public async Task PartChannel(string channelName)
        {
            channelName = NormalizeChannelName(channelName);

            if (!_channelService.Part(Context.ConnectionId, channelName, out var user) || user == null)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "Sistem", "En az bir kanalda kalmalısın.", "#Sohbet");
                return;
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, channelName);

            await Clients.Caller.SendAsync("ChannelParted", channelName);

            await Clients.Group(channelName).SendAsync(
                "ReceiveUserLeft",
                channelName,
                user.Nick
            );

            await Clients.Group(channelName).SendAsync(
                "ReceiveMessage",
                "Sistem",
                $"{user.Nick} ← {channelName} kanaldan ayrıldı.",
                channelName
            );

            // 🔥 ForceSwitchChannel kalktı, artık SetActiveChannel kullanıyoruz
            if (user.CurrentChannel != channelName)
            {
                await Clients.Caller.SendAsync("SetActiveChannel", user.CurrentChannel);
            }

            await SendChannelTopicInfo(channelName);
        }
        public override async Task OnConnectedAsync()
        {
            var nick = Context.GetHttpContext()?.Request.Query["nick"].ToString();

            if (string.IsNullOrWhiteSpace(nick))
            {
                nick = "RSohbet";
            }

            if (_onlineUserStore.NickExists(nick))
            {
                nick = nick + "_";
            }

            var defaultChannels = await _channelRegistrationRepository.GetDefaultJoinChannelsAsync();

            if (!defaultChannels.Any())
            {
                defaultChannels = new List<string> { "#Sohbet" };
            }

            // 🔥 KRİTİK: default kanalları normalize et
            defaultChannels = defaultChannels
                .Select(NormalizeChannelName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var user = new OnlineUser
            {
                ConnectionId = Context.ConnectionId,
                Nick = nick,
                CurrentChannel = defaultChannels.Contains("#Sohbet")
                    ? "#Sohbet"
                    : defaultChannels.First(),
                Channels = defaultChannels
            };

            foreach (var channel in user.Channels)
            {
                var registeredChannel = await _channelRegistrationRepository.GetByChannelAsync(channel);

                var role = _onlineUserStore.GetUsersInChannel(channel).Any()
                    ? ChannelRole.User
                    : ChannelRole.Op;

                user.ChannelRoles[channel] = registeredChannel != null
                    ? ChannelRole.User
                    : role;
            }

            _onlineUserStore.Add(user);

            foreach (var channel in user.Channels)
            {
                _onlineUserStore.AddUserToChannelIndex(user.ConnectionId, channel);
            }

            var isRegistered = await _nickServ.IsNickRegisteredAsync(user.Nick);

            if (isRegistered)
            {
                user.IsIdentified = false;
                user.IsWaitingForIdentify = true;
                user.IdentifyDeadlineAt = DateTime.UtcNow.AddSeconds(45);

                var timerId = Guid.NewGuid();
                user.IdentifyTimerId = timerId;

                await Clients.Caller.SendAsync(
                    "ReceiveMessage",
                    "NickServ",
                    $"{user.Nick} kayıtlı bir nick. 45 saniye içinde /ns identify şifre yazmalısınız.",
                    user.CurrentChannel
                );

                _ = CheckIdentifyTimeoutAsync(user.ConnectionId, timerId);
            }

            await Clients.Caller.SendAsync("SetNick", user.Nick);
            await Clients.Caller.SendAsync("SetActiveChannel", user.CurrentChannel);

            foreach (var channel in user.Channels)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, channel);

                await Clients.Caller.SendAsync("ChannelJoined", channel);

                await SendUserListToCaller(channel);

                var role = user.ChannelRoles.ContainsKey(channel)
                    ? (int)user.ChannelRoles[channel]
                    : (int)ChannelRole.User;

                await Clients.Group(channel).SendAsync(
                    "ReceiveUserJoined",
                    channel,
                    user.Nick,
                    role
                );

                await Clients.Group(channel).SendAsync(
                    "ReceiveMessage",
                    "Sistem",
                    $"{user.Nick} → {channel} kanalına bağlandı.",
                    channel
                );

                await SendChannelTopicInfo(channel);
            }

            await base.OnConnectedAsync();
        }
        private async Task CheckIdentifyTimeoutAsync(string connectionId, Guid timerId)
        {
            await Task.Delay(15000);

            var stillUser = _onlineUserStore.GetByConnectionId(connectionId);

            if (stillUser == null)
                return;

            if (stillUser.IsIdentified)
                return;

            if (stillUser.IdentifyTimerId != timerId)
                return;

            var oldNick = stillUser.Nick;
            var newNick = GenerateGuestNick();

            while (_onlineUserStore.NickExists(newNick))
            {
                newNick = GenerateGuestNick();
            }

            // 🔥 1. INDEX GÜNCELLE
            _onlineUserStore.UpdateNickIndex(connectionId, oldNick, newNick);

            // 🔥 2. USER GÜNCELLE
            stillUser.Nick = newNick;
            stillUser.IsNickProtectedGuest = true;
            stillUser.ProtectedNick = oldNick;
            stillUser.IsWaitingForIdentify = false;
            stillUser.IdentifyDeadlineAt = null;
            stillUser.IdentifyTimerId = null;

            // 🔥 3. KENDİ CLIENT
            await _hubContext.Clients.Client(stillUser.ConnectionId)
                .SendAsync("SetNick", newNick);

            // 🔥 4. DİĞERLERİNE NICK CHANGE
            foreach (var channel in stillUser.Channels)
            {
                await _hubContext.Clients.Group(channel).SendAsync(
                    "ReceiveNickChanged",
                    oldNick,
                    newNick
                );

                await _hubContext.Clients.Group(channel).SendAsync(
                    "ReceiveMessage",
                    "Sistem",
                    $"{oldNick} nickini {newNick} olarak değiştirdi (Şifre girilmedi).",
                    channel
                );
            }
        }
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var channels = _channelService.Quit(Context.ConnectionId, out var user);

            if (user != null)
            {
                foreach (var channel in channels)
                {
                    await Clients.Group(channel).SendAsync(
                        "ReceiveUserLeft",
                        channel,
                        user.Nick
                    );

                    await Clients.Group(channel).SendAsync(
                        "ReceiveMessage",
                        "Sistem",
                        $"{user.Nick} sunucudan ayrıldı.",
                        channel
                    );

                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, channel);
                }
            }

            foreach (var channel in channels)
            {
                await SendChannelTopicInfo(channel);
            }

            await base.OnDisconnectedAsync(exception);
        }
        public async Task SetChannelMode(string channelName, string modeText, string? targetNick = null)
        {
            var user = _onlineUserStore.GetByConnectionId(Context.ConnectionId);
            if (user == null) return;

            channelName = NormalizeChannelName(channelName);

            var dbChannel = await _channelRegistrationRepository.GetByChannelAsync(channelName);

            if (dbChannel == null)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "Sistem", "Kanal kayıtlı değil.", channelName);
                return;
            }

            var isFounder = user.UserAccountId != null &&
                            dbChannel.FounderUserAccountId == user.UserAccountId.Value;

            if (!isFounder)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "Sistem", "Yetkin yok.", channelName);
                return;
            }

            var userModeChars = new[] { 'q', 'y', 'a', 'o', 'z', 'h', 'v' };

            if (modeText.Length >= 2 && userModeChars.Contains(modeText[1]))
            {
                if (string.IsNullOrWhiteSpace(targetNick))
                {
                    await Clients.Caller.SendAsync(
                        "ReceiveMessage",
                        "Sistem",
                        $"Kullanım: /mode {channelName} {modeText} nick",
                        channelName
                    );
                    return;
                }

                var targetUser = _onlineUserStore.GetByNick(targetNick);

                if (targetUser == null || !targetUser.Channels.Contains(channelName))
                {
                    await Clients.Caller.SendAsync(
                        "ReceiveMessage",
                        "Sistem",
                        $"{targetNick} bu kanalda bulunamadı.",
                        channelName
                    );
                    return;
                }

                var action = modeText[0];
                var mode = modeText[1];

                if (action != '+' && action != '-')
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "Sistem", "Mode + veya - ile başlamalı.", channelName);
                    return;
                }

                var currentRole = targetUser.ChannelRoles.ContainsKey(channelName)
                    ? targetUser.ChannelRoles[channelName]
                    : ChannelRole.User;

                ChannelRole targetRole;
                string roleName;

                switch (mode)
                {
                    case 'q':
                        targetRole = ChannelRole.Founder;
                        roleName = "Founder";
                        break;

                    case 'y':
                        targetRole = ChannelRole.Owner;
                        roleName = "Owner";
                        break;

                    case 'a':
                        targetRole = ChannelRole.Sop;
                        roleName = "SOP";
                        break;

                    case 'o':
                        targetRole = ChannelRole.Op;
                        roleName = "OP";
                        break;

                    case 'z':
                        targetRole = ChannelRole.Vip;
                        roleName = "VIP";
                        break;

                    case 'h':
                        targetRole = ChannelRole.HalfOp;
                        roleName = "HalfOP";
                        break;

                    case 'v':
                        targetRole = ChannelRole.Voice;
                        roleName = "Voice";
                        break;

                    default:
                        await Clients.Caller.SendAsync("ReceiveMessage", "Sistem", $"Geçersiz kullanıcı mode: {mode}", channelName);
                        return;
                }

                ChannelRole newRole = action == '+'
                    ? targetRole
                    : ChannelRole.User;

                if (action == '+' && currentRole == targetRole)
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "Sistem", $"{targetUser.Nick} zaten {roleName}.", channelName);
                    return;
                }

                if (action == '-' && currentRole != targetRole)
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "Sistem", $"{targetUser.Nick} üzerinde {roleName} yok.", channelName);
                    return;
                }

                targetUser.ChannelRoles[channelName] = newRole;

                await Clients.Group(channelName).SendAsync(
                    "ReceiveUserRoleChanged",
                    channelName,
                    targetUser.Nick,
                    (int)newRole
                );

                await Clients.Group(channelName).SendAsync(
                    "ReceiveMessage",
                    "ChanServ",
                    $"{user.Nick}, {targetUser.Nick} için {modeText} uyguladı. ({roleName})",
                    channelName
                );

                return;
            }

            var currentModes = dbChannel.Modes
                .Replace("+", "")
                .ToHashSet();

            var result = _channelModeService.SetMode(currentModes, modeText);

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "Sistem", result.Message, channelName);
                return;
            }

            dbChannel.Modes = result.ModesText;

            await _channelRegistrationRepository.UpdateAsync(dbChannel);

            await Clients.Group(channelName).SendAsync(
                "ReceiveMessage",
                "ChanServ",
                $"{user.Nick} mode değiştirdi: {modeText} ({result.ModesText})",
                channelName
            );

            var userCount = _onlineUserStore.GetUsersInChannel(channelName).Count;

            await Clients.Group(channelName).SendAsync(
                "ReceiveTopicInfo",
                channelName,
                userCount,
                result.ModesText,
                dbChannel.Topic ?? "Sohbet Kanalına Hoşgeldiniz",
                ""
            );
        }
        public async Task SendChannelMessageDirect(string channelName, string message)
        {
            var user = _onlineUserStore.GetByConnectionId(Context.ConnectionId);
            if (user == null) return;

            channelName = NormalizeChannelName(channelName);

            var dbChannel = await _channelRegistrationRepository.GetByChannelAsync(channelName);

            if (dbChannel == null)
            {
                await Clients.Caller.SendAsync(
                    "ReceiveMessage",
                    "Sistem",
                    "Kanal bulunamadı.",
                    channelName
                );
                return;
            }

            // 🔥 +n kontrolü (ARTIK GERÇEK)
            if (dbChannel.Modes.Contains("n") && !user.Channels.Contains(channelName))
            {
                await Clients.Caller.SendAsync(
                    "ReceiveMessage",
                    "Sistem",
                    $"{channelName} kanalı +n modunda. Kanala dışarıdan mesaj gönderemezsiniz.",
                    user.CurrentChannel
                );
                return;
            }

            await Clients.Group(channelName).SendAsync(
                "ReceiveMessage",
                user.Nick,
                message,
                channelName
            );
        }
        public async Task SetChannelTopic(string channelName, string topic)
        {
            var user = _onlineUserStore.GetByConnectionId(Context.ConnectionId);
            if (user == null) return;

            channelName = NormalizeChannelName(channelName);

            var dbChannel = await _channelRegistrationRepository.GetByChannelAsync(channelName);

            if (dbChannel == null)
            {
                await Clients.Caller.SendAsync(
                    "ReceiveMessage",
                    "Sistem",
                    "Kanal kayıtlı değil.",
                    channelName
                );
                return;
            }

            var modes = dbChannel.Modes?.Replace("+", "") ?? "";

            var isFounder = user.UserAccountId != null &&
                            dbChannel.FounderUserAccountId == user.UserAccountId;

            var role = user.ChannelRoles.ContainsKey(channelName)
                ? user.ChannelRoles[channelName]
                : ChannelRole.User;

            var isOp = role >= ChannelRole.Op;

            // 🔥 +t kontrolü
            if (modes.Contains('t') && !isFounder && !isOp)
            {
                await Clients.Caller.SendAsync(
                    "ReceiveMessage",
                    "Sistem",
                    "Bu kanalın konusu sadece yetkililer tarafından değiştirilebilir (+t aktif).",
                    channelName
                );
                return;
            }

            dbChannel.Topic = topic;

            await _channelRegistrationRepository.UpdateAsync(dbChannel);

            await Clients.Group(channelName).SendAsync(
                "ReceiveMessage",
                "ChanServ",
                $"{user.Nick} kanal konusunu değiştirdi: {topic}",
                channelName
            );

            await SendChannelTopicInfo(channelName);
        }
        public async Task RequestChannelTopicInfo(string channelName)
        {
            var user = _onlineUserStore.GetByConnectionId(Context.ConnectionId);
            if (user == null) return;

            channelName = NormalizeChannelName(channelName);

            if (!user.Channels.Contains(channelName))
                return;

            await SendChannelTopicInfo(channelName);
        }
    }
}
