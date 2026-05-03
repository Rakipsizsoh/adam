using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyIRC.Domain.Entities.Channels
{
    public class ChannelRegistration
    {
        public int Id { get; set; }

        public string ChannelName { get; set; } = string.Empty;

        public int FounderUserAccountId { get; set; }

        public string PasswordHash { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string FounderNick { get; set; } = string.Empty;

        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

        public string? Topic { get; set; }

        public bool IsTopicLocked { get; set; } = false;

        public bool IsActive { get; set; } = true;

        // 🔥 MODE
        public string Modes { get; set; } = "+";

        // 🔥 AUTO JOIN (YENİ)
        public bool IsDefaultJoin { get; set; } = false;
    }
}