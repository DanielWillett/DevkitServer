using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevkitServer.Players;
public class EditorPlayer : MonoBehaviour
{
    public ulong Steam64 { get; }
    public ITransportConnection Connection { get; }
}
