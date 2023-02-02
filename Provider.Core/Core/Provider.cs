using RuleProviderFactory.Core.DataObject;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuleProviderFactory.Core.Core
{
    public abstract class Provider : IProvider
    {
        public abstract string Execute(RuleProviderRequest request);
    }
}
