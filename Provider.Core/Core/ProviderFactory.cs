using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RuleProviderFactory.Core.Core;
using System.Reflection;

namespace RuleProviderFactory.Core.Core
{
    public static class ProviderFactory
    {
        private static IEnumerable<System.Type> LoadAllRuleProvider()
        {
            var baseType = typeof(Provider);
            var assembly = Assembly.GetExecutingAssembly();

            return assembly.GetTypes().Where(t => t.IsSubclassOf(baseType));
        }

        public static Provider GetProvider(string providerName)
        {
            if (providerName != null)
                providerName = providerName.ToLower();
            else
                providerName = "usp_ircommunicationexclusion";

            providerName = providerName.Replace("iprovider.", "");
            Provider providerInstance = null;
            var allProviders = LoadAllRuleProvider();
            if (allProviders != null)
            {
                var provider = allProviders.Where(x => x.Name.ToLower() == providerName).FirstOrDefault();
                if (provider != null)
                {
                    Type t = Type.GetType(provider.FullName);
                    return providerInstance = (Provider)Activator.CreateInstance(t);
                }
            }

            return providerInstance;

        }
    }
}
