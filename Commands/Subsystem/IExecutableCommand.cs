using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevkitServer.Commands.Subsystem;
public interface IExecutableCommand
{
    public void Execute(string interaction);
}
