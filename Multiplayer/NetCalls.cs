﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevkitServer.Multiplayer;
internal enum NetCalls : ushort
{
    SendLog = 1,
    SendMovementPacket = 2,
    FlushEditBuffer = 3
}
