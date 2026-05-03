using System;
using System.Collections.Generic;
using System.Linq;

namespace MyIRC.Application.Services.ChannelModes
{
    public class ChannelModeService
    {
        private readonly char[] _allowedModes = new[]
{
    // 🔥 mesaj / kanal kontrol
    'm', // moderated
    'M', // only identified
    'n', // no external msg
    't', // topic lock
    'i', // invite only
    's', // secret
    'p', // private

    // 🔥 kanal ayar
    'k', // key (şifre)
    'l', // limit

    // 🔥 ban / exception
    'b', // ban
    'e', // exception
    'I', // invite exception

    // 🔥 yetki (SENİN SİSTEM)
    'q', // founder (.)
    'y', // owner (~)
    'a', // sop (&)
    'o', // op (@)
    'h', // halfop (%)
    'v', // voice (+)
    'z', // vip (!)

    // 🔥 sistem
    'r'  // registered
};

        public (bool Success, string Message, string ModesText) SetMode(
    HashSet<char> currentModes,
    string modeText)
        {
            if (string.IsNullOrWhiteSpace(modeText) || modeText.Length < 2)
                return (false, "Geçersiz mode.", "+" + string.Join("", currentModes.OrderBy(x => x)));

            char? action = null;
            var changed = new List<string>();
            var skipped = new List<string>();

            foreach (var ch in modeText)
            {
                if (ch == '+' || ch == '-')
                {
                    action = ch;
                    continue;
                }

                if (action == null)
                    return (false, "Mode + veya - ile başlamalı.", "+" + string.Join("", currentModes.OrderBy(x => x)));

                if (!_allowedModes.Contains(ch))
                    return (false, $"Geçersiz mode: {ch}", "+" + string.Join("", currentModes.OrderBy(x => x)));

                if (action == '+')
                {
                    if (currentModes.Contains(ch))
                    {
                        skipped.Add($"+{ch}");
                        continue;
                    }

                    currentModes.Add(ch);
                    changed.Add($"+{ch}");
                }
                else
                {
                    if (!currentModes.Contains(ch))
                    {
                        skipped.Add($"-{ch}");
                        continue;
                    }

                    currentModes.Remove(ch);
                    changed.Add($"-{ch}");
                }
            }

            var modesText = "+" + string.Join("", currentModes.OrderBy(x => x));

            if (!changed.Any())
            {
                return (false, $"Zaten bu modda: {string.Join(" ", skipped)}", modesText);
            }

            return (true, $"Mode değişti: {string.Join(" ", changed)}", modesText);
        }
    }
}