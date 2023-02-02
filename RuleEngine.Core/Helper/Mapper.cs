using RuleEngine.DataObject;
using RuleProviderFactory.Core.DataObject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RuleEngine.Core.Helper
{
    public class Mapper
    {
        public RuleProviderRequest GetRuleProviderRequest(Request request)
        {
            RuleProviderRequest providerRequest = new RuleProviderRequest()
            {
                ClaimNo = request.ClaimNo,
                AgentContact = request.AgentContact,
                AgentEmail = request.AgentEmail,
                Amount = request.Amount,
                ApprEmail = request.ApprEmail,
                AreaCode = request.AreaCode,
                ClaimType = request.ClaimType,
                CorpId = request.CorpId,
                CustomerContact = request.CustomerContact,
                CustomerEmail = request.CustomerEmail,
                DBType = request.DBType,
                FromState = request.FromState,
                HospitalContact = request.HospitalContact,
                HospitalEmail = request.HospitalEmail,
                HRContact = request.HRContact,
                HREmail = request.HREmail,
                InsCompId = request.InsCompId,
                InsOffEmail = request.InsOffEmail,
                InsurerStatusId = request.InsurerStatusId,
                IsNEFT = request.IsNEFT,
                NEFTAcEmail = request.NEFTAcEmail,
                PolicyHolderContact = request.PolicyHolderContact,
                PolicyHolderEmail = request.PolicyHolderEmail,
                PolicyId = request.PolicyId,
                PolicyNo = request.PolicyNo,
                PolicyTypeId = request.PolicyTypeId,
                SchemeId = request.SchemeId,
                ToState = request.ToState
            };
            
            return providerRequest;
        }
    }
}
