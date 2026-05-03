using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyIRC.Domain.Enums;

public enum ChannelRole
{
    User = 0,
    Voice = 10,
    HalfOp = 20,
    Vip = 30,
    Op = 40,
    Sop = 50,
    Owner = 60,
    Founder = 70
}