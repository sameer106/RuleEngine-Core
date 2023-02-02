using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RuleProviderFactory.Core.Core;
using System.Data;
using System.Data.SqlClient;
using RuleProviderFactory.Core.Helper;
using RuleProviderFactory.Core.DataObject;

namespace RuleProviderFactory.Core.ExtendedProviders
{
    public class usp_ircommunicationexclusion : Provider
    {
        DBHelper dbHelper = null;
        public usp_ircommunicationexclusion()
        {
            dbHelper = new DBHelper();
        }

        public override string Execute(RuleProviderRequest request)
        {
            StringBuilder sb = new StringBuilder();
            string result = string.Empty;
            DataSet ds = new DataSet();

            using (SqlConnection Con = new SqlConnection(dbHelper.GetconnectionStringFactory(request.DBType)))
            {
                using (SqlCommand cmd = new SqlCommand("USP_IRCommunicationExclusion", Con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Polid", request.PolicyId);
                    SqlDataAdapter da = new SqlDataAdapter(cmd);
                    da.Fill(ds);
                }
                Con.Close();
            }

            bool needSeprator = false;
            foreach (DataRow row in ds.Tables[0].Rows)
            {
                IEnumerable<string> fields = row.ItemArray.Select(field => field.ToString());
                if(needSeprator)
                    sb.AppendLine(",");

                sb.AppendLine(string.Join(",", fields));
                needSeprator = true;
               
            }

            result = sb.ToString();
            return result;
        }
    }
}
