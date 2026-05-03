using MyIRC.Domain.Enums;

namespace MyIRC.Domain.Entities.Irc
{
    public class OnlineUser
    {
        public string ConnectionId { get; set; } = string.Empty;

        public string Nick { get; set; } = string.Empty;

        public string CurrentChannel { get; set; } = string.Empty;

        public List<string> Channels { get; set; } = new();

        // Kanal bazlı rol bilgisi
        public Dictionary<string, ChannelRole> ChannelRoles { get; set; } = new();

        // NickServ identify durumu
        public bool IsIdentified { get; set; } = false;

        // Hangi kayıtlı hesaba identify oldu?
        public int? UserAccountId { get; set; }

        // Identify olduğu nick
        public string? IdentifiedNick { get; set; }

        // Identify zamanı
        public DateTime? IdentifiedAt { get; set; }

        // Nick protect sebebiyle misafir yapıldı mı?
        public bool IsNickProtectedGuest { get; set; } = false;

        // Asıl almak istediği kayıtlı nick
        public string? ProtectedNick { get; set; }

        // Identify bekleme durumu
        public bool IsWaitingForIdentify { get; set; } = false;

        // Identify için son süre
        public DateTime? IdentifyDeadlineAt { get; set; }

        // Identify timer iptal kontrolü
        public Guid? IdentifyTimerId { get; set; }

        // Yanlış identify deneme sayısı
        public int IdentifyFailedAttempts { get; set; } = 0;
    }
}