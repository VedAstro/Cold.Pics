using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace API
{
    public readonly record struct ConvertCommand(ConvertOptions Options, byte[] Image);
}
