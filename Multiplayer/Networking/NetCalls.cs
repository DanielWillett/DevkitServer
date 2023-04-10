using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevkitServer.Multiplayer.Networking;
internal enum NetCalls : ushort
{
    SendLog = 1,
    FlushEditBuffer = 2,
    RequestLevel = 3,
    StartSendLevel = 4,
    SendLevel = 5,
    EndSendLevel = 6,
    RequestLevelPackets = 7
}
