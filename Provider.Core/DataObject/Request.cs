using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuleProviderFactory.Core.DataObject
{
    public class RuleProviderRequest
    {
        public int FromState { get; set; }
        public int ToState { get; set; }
        public long ClaimNo { get; set; }
        public int CorpId { get; set; }
        public int SchemeId { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerContact { get; set; }

        public string HREmail { get; set; }
        public string HRContact { get; set; }

        public string HospitalEmail { get; set; }
        public string HospitalContact { get; set; }

        public string AgentEmail { get; set; }
        public string AgentContact { get; set; }

        public string PolicyHolderEmail { get; set; }
        public string PolicyHolderContact { get; set; }
        public string PolicyNo { get; set; }
        public string PolicyTypeId { get; set; }
        public string AreaCode { get; set; }
        public decimal Amount { get; set; }

        public int InsCompId { get; set; }
        public string InsurerStatusId { get; set; }
        public string ClaimType { get; set; }
        public string PolicyId { get; set; }
        public bool IsNEFT { get; set; }


        public string ApprEmail { get; set; }
        public string NEFTAcEmail { get; set; }
        public string InsOffEmail { get; set; }
        public string DBType { get; set; }

    }
}
