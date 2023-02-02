using RuleProviderFactory.Core.Core;
using RuleProviderFactory.Core.Helper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace RuleProviderFactory.Core.ExtendedProviders
{
    public class usp_getfromemailid : Provider
    {
        DBHelper dbHelper = null;
        public usp_getfromemailid()
        {
            dbHelper = new DBHelper();
        }

        public override string Execute(DataObject.RuleProviderRequest request)
        {
            string result = string.Empty;
            DataSet ds = new DataSet();

            using (SqlConnection Con = new SqlConnection(dbHelper.GetconnectionStringFactory(request.DBType)))
            {
                using (SqlCommand cmd = new SqlCommand("usp_getfromemailid", Con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Clmno", request.ClaimNo);
                    SqlDataAdapter da = new SqlDataAdapter(cmd);
                    da.Fill(ds);
                }
                Con.Close();
            }

            if (ds != null && ds.Tables != null && ds.Tables.Count > 0 && ds.Tables[0].Rows != null)
                result = string.Join(",", ds.Tables[0].Rows[0].ItemArray);
                
            return result;
        }
    }
}
