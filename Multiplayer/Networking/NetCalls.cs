using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevkitServer.Multiplayer.Networking;
internal enum NetCalls : ushort
{
    SendLog = 1,
    SendMovementPacket = 2,
    FlushEditBuffer = 3,
    RequestLevel = 4,
    StartSendLevel = 5,
    SendLevel = 6,
    EndSendLevel = 7,
    RequestLevelPackets = 8
}
