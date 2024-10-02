using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS.ApiService
{
    public record ResourceRequestBase(string IpAddress, string ResourceId)
    {
    }
}
