using ServiceStack.Logging;
using ServiceStack.Logging.Log4Net;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.SqlServer;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuleProviderFactory.Core.Helper
{
    public class DBHelper
    {
        private static ILogFactory _LogFactory = null;
        private static IDbConnectionFactory _DbConnectionFactory = null;
        string RulesConnectionString = ConfigurationManager.AppSettings.Get("RulesDBConnString");
        string MAConnectionString = ConfigurationManager.AppSettings.Get("MAConnString");
        string MAUHSConnectionString = ConfigurationManager.AppSettings.Get("MAUHSConnString");

        public DBHelper()
        {
            log4net.Config.XmlConfigurator.Configure();
            _LogFactory = new Log4NetFactory(true);
        }

        public IDbConnectionFactory GetconnectionFactory(string DBType)
        {
            if (!string.IsNullOrEmpty(DBType) && string.Equals(DBType, "RULES", StringComparison.InvariantCultureIgnoreCase))
            {
                _DbConnectionFactory = new OrmLiteConnectionFactory(RulesConnectionString, SqlServerOrmLiteDialectProvider.Instance);
                return _DbConnectionFactory;
            }

            if (!string.IsNullOrEmpty(DBType) && string.Equals(DBType, "MAUHS", StringComparison.InvariantCultureIgnoreCase))
            {
                _DbConnectionFactory = new OrmLiteConnectionFactory(MAConnectionString, SqlServerOrmLiteDialectProvider.Instance);
                return _DbConnectionFactory;
            }
            else
            {
                _DbConnectionFactory = new OrmLiteConnectionFactory(MAUHSConnectionString, SqlServerOrmLiteDialectProvider.Instance);
                return _DbConnectionFactory;
            }
        }

        public string GetconnectionStringFactory(string DBType)
        {
            if (!string.IsNullOrEmpty(DBType) && string.Equals(DBType, "RULES", StringComparison.InvariantCultureIgnoreCase))
            {
                //_DbConnectionFactory = new OrmLiteConnectionFactory(RulesConnectionString, SqlServerOrmLiteDialectProvider.Instance);
                return RulesConnectionString;
            }

            if (!string.IsNullOrEmpty(DBType) && string.Equals(DBType, "MAUHS", StringComparison.InvariantCultureIgnoreCase))
            {
                //_DbConnectionFactory = new OrmLiteConnectionFactory(MAConnectionString, SqlServerOrmLiteDialectProvider.Instance);
                return MAConnectionString;
            }
            else
            {
                //_DbConnectionFactory = new OrmLiteConnectionFactory(MAUHSConnectionString, SqlServerOrmLiteDialectProvider.Instance);
                return MAUHSConnectionString;
            }
        }

    }
}
