using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Configuration;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Schema;
using Newtonsoft.Json.Bson;
using RuleEngine.Core.Domain;
using RuleEngine.Core.Helper;
using RuleEngine.DataObject;
using ServiceStack.Common;
//using ServiceStack.Common.Extensions;
using ServiceStack.Logging;
using ServiceStack.Logging.Log4Net;
using ServiceStack.Text;
using System.Reflection;
using RuleProviderFactory.Core.DataObject;
using RuleProviderFactory.Core.Core;

namespace RuleEngine.Core
{
    public class RuleProvider
    {
        #region "Private Variables"

        private static List<CommunicationRules> communicationRules;
        private static List<CorporateRules> corporateRules;
        private static object _locker = new object();
        private static bool IsInitializationDone = false;
        private static ILogFactory _LogFactory = null;
        
        private DateTime cutOffDateTime = (DateTime)System.Data.SqlTypes.SqlDateTime.MinValue;
        private long ThreadSleepTimeInMS = 1 * 60 * 1000;

        #endregion "Private Variables"

        static RuleProvider()
        {
            communicationRules = new List<CommunicationRules>();
            corporateRules = new List<CorporateRules>();

            _LogFactory = new Log4NetFactory(true);
        }

        public RuleProvider()
        {
            if(!IsInitializationDone)
            {
                lock(_locker)
                {
                    if(!IsInitializationDone)
                    {
                        prepareVariables();
                        IsInitializationDone = true;
                    }
                }
            }
        }
        
        /// <summary>
        /// fetch the rules form db
        /// get the ruleSet of CorpId or SchemeID
        /// Run the filter and return bool
        /// if filter return true get the action and update the final actionObject value if action value !=null
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public List<RuleAction> Execute(Request request)
        {
            List<RuleAction> resultRuleActionList = new List<RuleAction>();
            //RuleAction resultRuleAction = new RuleAction();
            PrecompiledRules preCompiledRules = new PrecompiledRules();

            try
            {
                var corporateStateRules = GetRuleSetsByCorpSchemaIdAndToState(request);

                //if no rule than get default rule Set for corporate
                //Default CorporateID = 0
                if (corporateStateRules == null)
                {
                    var CorpId = request.CorpId;
                    request.CorpId = 0;
                    corporateStateRules = GetRuleSetsByCorpSchemaIdAndToState(request);
                    request.CorpId = CorpId;
                }

                if (corporateStateRules == null)
                    return resultRuleActionList;

                foreach (var rules in corporateStateRules)
                {
                    if (rules.filter == null || rules.filter.ruleExpression == null || rules.filter.ruleExpression.Count == 0)
                        continue;
                    //call SP to replace the IProvider with DB CSV value
                    Provider_ExecuteSP_Filter(request, rules.filter.ruleExpression);

                    var CompiledRule = preCompiledRules.CompileRule(request, rules.filter.ruleExpression);

                    //it will evaluate the list of RuleExpression as And condition
                    if (!CompiledRule.Any(x => !x.Invoke(request)))
                    {
                        if (_LogFactory != null)
                            _LogFactory.GetLogger("Global")
                                .Info("RULE_ENGINE: Validate the Rule as True for " + rules.SerializeToString());

                        ComposeFinalAction(rules.action, resultRuleActionList);

                    }

                    
                }
                //call SP to replace the IProvider with DB CSV value
                Provider_ExecuteSP_Action(request, resultRuleActionList);

                ReplaceCustomerHRTagsByValue(request, resultRuleActionList);
            }
            catch (Exception ex)
            {
                if (_LogFactory != null)
                    _LogFactory.GetLogger("Global").Error("RULE_ENGINE: Failed to Execute Rule Engine due to exception", ex);
            }

            return resultRuleActionList;

        }


        private void Provider_ExecuteSP_Filter(Request request, List<RuleExpression> ruleExpression)
        {
            foreach(var ruleExp in ruleExpression)
            {
                if(ruleExp!=null && ruleExp.value !=null && ruleExp.value.ToLower().Contains("iprovider"))
                {
                   var provider = ProviderFactory.GetProvider(ruleExp.value);
                   string DBResult = string.Empty;
                   RuleProviderRequest ruleProviderRequest = new RuleProviderRequest();
                   Mapper mapper = new Mapper();
                   ruleProviderRequest = mapper.GetRuleProviderRequest(request);
                   DBResult = provider.Execute(ruleProviderRequest);

                   if (!string.IsNullOrEmpty(DBResult))
                       ruleExp.value = DBResult;
                       //ruleExpression.Where(x => x.value.ToLower() == ruleExp.value).Select(z => z.value = DBResult);



                }
            }
        }

        class KeyValuePair
        {
            public string Key { get; set; }
            public string Value { get; set; }
        }

        private void Provider_ExecuteSP_Action(Request request, List<RuleAction> ruleActionList)
        {

            List<KeyValuePair> keyValue = new List<KeyValuePair>();
            foreach (var ruleAction in ruleActionList)
            {
                var ruleActionKeyValueDict = ruleAction.ToStringDictionary();
                //var properties = type.GetProperties();
                //var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var dict in ruleActionKeyValueDict)
                {
                    if (dict.Value != null && dict.Value.ToLower().Contains("iprovider"))
                    {
                        keyValue.Add(new KeyValuePair() { Key = dict.Key, Value = dict.Value });
                    }
                }
                
                foreach (var item in keyValue)
                {
                    RuleProviderRequest ruleProviderRequest = new RuleProviderRequest();
                    Mapper mapper = new Mapper();
                    ruleProviderRequest = mapper.GetRuleProviderRequest(request);

                    string DBResult = string.Empty;
                    var provider = ProviderFactory.GetProvider(item.Value);
                    DBResult = provider.Execute(ruleProviderRequest);

                    if (!string.IsNullOrEmpty(DBResult))
                    {
                        PropertyInfo propertyInfo = ruleAction.GetType().GetProperty(item.Key);
                        propertyInfo.SetValue(ruleAction, Convert.ChangeType(DBResult, propertyInfo.PropertyType), null);
                    }
                }

            }

            

        }

        private void ReplaceCustomerHRTagsByValue(Request request, List<RuleAction> ruleActionList)
        {
            CustomExtension customExt = new CustomExtension();

            foreach (var ruleAction in ruleActionList)
            {
                //Replace customer tag by value
                if (ruleAction.toEmailHashSet != null) ruleAction.toEmailHashSet = customExt.ReplaceValue(ruleAction.toEmailHashSet, request.CustomerEmail, "customertag");
                if (ruleAction.ccEmailHashSet != null) ruleAction.ccEmailHashSet = customExt.ReplaceValue(ruleAction.ccEmailHashSet, request.CustomerEmail, "customertag");
                if (ruleAction.bccEmailHashSet != null) ruleAction.bccEmailHashSet = customExt.ReplaceValue(ruleAction.bccEmailHashSet, request.CustomerEmail, "customertag");
                if (ruleAction.contactNoHashSet != null) ruleAction.contactNoHashSet = customExt.ReplaceValue(ruleAction.contactNoHashSet, request.CustomerContact, "customertag");

                //replace HR tag by value
                if (ruleAction.toEmailHashSet != null) ruleAction.toEmailHashSet = customExt.ReplaceValue(ruleAction.toEmailHashSet, request.HREmail, "hrtag");
                if (ruleAction.ccEmailHashSet != null) ruleAction.ccEmailHashSet = customExt.ReplaceValue(ruleAction.ccEmailHashSet, request.HREmail, "hrtag");
                if (ruleAction.bccEmailHashSet != null) ruleAction.bccEmailHashSet = customExt.ReplaceValue(ruleAction.bccEmailHashSet, request.HREmail, "hrtag");
                if (ruleAction.contactNoHashSet != null) ruleAction.contactNoHashSet = customExt.ReplaceValue(ruleAction.contactNoHashSet, request.HRContact, "hrtag");

                //replace Hospital tag by value
                if (ruleAction.toEmailHashSet != null) ruleAction.toEmailHashSet = customExt.ReplaceValue(ruleAction.toEmailHashSet, request.HospitalEmail, "hospitaltag");
                if (ruleAction.ccEmailHashSet != null) ruleAction.ccEmailHashSet = customExt.ReplaceValue(ruleAction.ccEmailHashSet, request.HospitalEmail, "hospitaltag");
                if (ruleAction.bccEmailHashSet != null) ruleAction.bccEmailHashSet = customExt.ReplaceValue(ruleAction.bccEmailHashSet, request.HospitalEmail, "hospitaltag");
                if (ruleAction.contactNoHashSet != null) ruleAction.contactNoHashSet = customExt.ReplaceValue(ruleAction.contactNoHashSet, request.HRContact, "hospitaltag");

                //replace PolicyHolder tag by value
                if (ruleAction.toEmailHashSet != null) ruleAction.toEmailHashSet = customExt.ReplaceValue(ruleAction.toEmailHashSet, request.PolicyHolderEmail, "policyholdertag");
                if (ruleAction.ccEmailHashSet != null) ruleAction.ccEmailHashSet = customExt.ReplaceValue(ruleAction.ccEmailHashSet, request.PolicyHolderEmail, "policyholdertag");
                if (ruleAction.bccEmailHashSet != null) ruleAction.bccEmailHashSet = customExt.ReplaceValue(ruleAction.bccEmailHashSet, request.PolicyHolderEmail, "policyholdertag");
                if (ruleAction.contactNoHashSet != null) ruleAction.contactNoHashSet = customExt.ReplaceValue(ruleAction.contactNoHashSet, request.PolicyHolderContact, "policyholdertag");

                //replace Appr tag by value
                if (ruleAction.toEmailHashSet != null) ruleAction.toEmailHashSet = customExt.ReplaceValue(ruleAction.toEmailHashSet, request.ApprEmail, "apprtag");
                if (ruleAction.ccEmailHashSet != null) ruleAction.ccEmailHashSet = customExt.ReplaceValue(ruleAction.ccEmailHashSet, request.ApprEmail, "apprtag");
                if (ruleAction.bccEmailHashSet != null) ruleAction.bccEmailHashSet = customExt.ReplaceValue(ruleAction.bccEmailHashSet, request.ApprEmail, "apprtag");
                if (ruleAction.contactNoHashSet != null) ruleAction.contactNoHashSet = customExt.ReplaceValue(ruleAction.contactNoHashSet, request.PolicyHolderContact, "apprtag");

                //replace NEFTAc tag by value
                if (ruleAction.toEmailHashSet != null) ruleAction.toEmailHashSet = customExt.ReplaceValue(ruleAction.toEmailHashSet, request.NEFTAcEmail, "neftactag");
                if (ruleAction.ccEmailHashSet != null) ruleAction.ccEmailHashSet = customExt.ReplaceValue(ruleAction.ccEmailHashSet, request.NEFTAcEmail, "neftactag");
                if (ruleAction.bccEmailHashSet != null) ruleAction.bccEmailHashSet = customExt.ReplaceValue(ruleAction.bccEmailHashSet, request.NEFTAcEmail, "neftactag");
                if (ruleAction.contactNoHashSet != null) ruleAction.contactNoHashSet = customExt.ReplaceValue(ruleAction.contactNoHashSet, request.PolicyHolderContact, "neftactag");

                //replace InsOff tag by value
                if (ruleAction.toEmailHashSet != null) ruleAction.toEmailHashSet = customExt.ReplaceValue(ruleAction.toEmailHashSet, request.InsOffEmail, "insofftag");
                if (ruleAction.ccEmailHashSet != null) ruleAction.ccEmailHashSet = customExt.ReplaceValue(ruleAction.ccEmailHashSet, request.InsOffEmail, "insofftag");
                if (ruleAction.bccEmailHashSet != null) ruleAction.bccEmailHashSet = customExt.ReplaceValue(ruleAction.bccEmailHashSet, request.InsOffEmail, "insofftag");
                if (ruleAction.contactNoHashSet != null) ruleAction.contactNoHashSet = customExt.ReplaceValue(ruleAction.contactNoHashSet, request.PolicyHolderContact, "policyholdertag");

                //Replace agent tag by value
                if (ruleAction.toEmailHashSet != null) ruleAction.toEmailHashSet = customExt.ReplaceValue(ruleAction.toEmailHashSet, request.CustomerEmail, "agenttag");
                if (ruleAction.ccEmailHashSet != null) ruleAction.ccEmailHashSet = customExt.ReplaceValue(ruleAction.ccEmailHashSet, request.CustomerEmail, "agenttag");
                if (ruleAction.bccEmailHashSet != null) ruleAction.bccEmailHashSet = customExt.ReplaceValue(ruleAction.bccEmailHashSet, request.CustomerEmail, "agenttag");
                if (ruleAction.contactNoHashSet != null) ruleAction.contactNoHashSet = customExt.ReplaceValue(ruleAction.contactNoHashSet, request.CustomerContact, "agenttag");


                //remove empty or null value
                if (ruleAction.toEmailHashSet != null) ruleAction.toEmailHashSet.RemoveAll(s => string.IsNullOrWhiteSpace(s));
                if (ruleAction.ccEmailHashSet != null) ruleAction.ccEmailHashSet.RemoveAll(s => string.IsNullOrWhiteSpace(s));
                if (ruleAction.bccEmailHashSet != null) ruleAction.bccEmailHashSet.RemoveAll(s => string.IsNullOrWhiteSpace(s));
                if (ruleAction.contactNoHashSet != null) ruleAction.contactNoHashSet.RemoveAll(s => string.IsNullOrWhiteSpace(s));

                //remove duplicate values
                if (ruleAction.toEmailHashSet != null) ruleAction.toEmailHashSet = customExt.RemoveDuplicate(ruleAction.toEmailHashSet);
                if (ruleAction.ccEmailHashSet != null) ruleAction.ccEmailHashSet = customExt.RemoveDuplicate(ruleAction.ccEmailHashSet);
                if (ruleAction.bccEmailHashSet != null) ruleAction.bccEmailHashSet = customExt.RemoveDuplicate(ruleAction.bccEmailHashSet);
                if (ruleAction.contactNoHashSet != null) ruleAction.contactNoHashSet = customExt.RemoveDuplicate(ruleAction.contactNoHashSet);
            }
          }
        
        private void ComposeFinalAction(RuleAction ruleAction, RuleAction resultRuleAction)
        {
            CustomExtension customerExt = new CustomExtension();
            resultRuleAction.LetterRequired = ruleAction.LetterRequired;

            if (ruleAction.sendEmail)
            {
                resultRuleAction.sendEmail = true;

                resultRuleAction.toEmailHashSet = customerExt.AddListSet(resultRuleAction.toEmailHashSet, ruleAction.toEmailHashSet);
                resultRuleAction.ccEmailHashSet = customerExt.AddListSet(resultRuleAction.ccEmailHashSet, ruleAction.ccEmailHashSet);
                resultRuleAction.bccEmailHashSet = customerExt.AddListSet(resultRuleAction.bccEmailHashSet, ruleAction.bccEmailHashSet);
                resultRuleAction.attachmentHashSet = customerExt.AddListSet(resultRuleAction.attachmentHashSet, ruleAction.attachmentHashSet);

                if (string.IsNullOrEmpty(resultRuleAction.fromEmailId) && !string.IsNullOrEmpty(ruleAction.fromEmailId))
                    resultRuleAction.fromEmailId = ruleAction.fromEmailId;

                if(ruleAction.emailTemplateId > 0)
                    resultRuleAction.emailTemplateId = ruleAction.emailTemplateId;

                if (ruleAction.letterId > 0)
                    resultRuleAction.letterId = ruleAction.letterId;
            }
            else 
            {
                //except email mention in sendEmail = false

                customerExt.ExceptWithNullCheck(resultRuleAction.toEmailHashSet, ruleAction.toEmailHashSet);
                customerExt.ExceptWithNullCheck(resultRuleAction.ccEmailHashSet, ruleAction.ccEmailHashSet);
                customerExt.ExceptWithNullCheck(resultRuleAction.bccEmailHashSet, ruleAction.bccEmailHashSet);
                customerExt.ExceptWithNullCheck(resultRuleAction.attachmentHashSet, ruleAction.attachmentHashSet);
            }

            if (ruleAction.sendSMS)
            {
                resultRuleAction.sendSMS = true;

                resultRuleAction.contactNoHashSet = customerExt.AddListSet(resultRuleAction.contactNoHashSet, ruleAction.contactNoHashSet);
                if(ruleAction.smsTemplateId > 0)
                    resultRuleAction.smsTemplateId = ruleAction.smsTemplateId;

            }
            else
            {
                if (resultRuleAction.contactNoHashSet != null && ruleAction.contactNoHashSet != null)
                    customerExt.ExceptWithNullCheck(resultRuleAction.contactNoHashSet, ruleAction.contactNoHashSet);
            }
        }

        private void ComposeFinalAction(List<RuleAction> ruleActionList, List<RuleAction> resultRuleActionList)
        {
            bool isNewObejct = false;
            CustomExtension customerExt = new CustomExtension();
            RuleAction resultRuleAction = null;
            foreach (var ruleAction in ruleActionList)
            {
                //Is the action with emailTemplate already there in finalActionList or else create the object and than add in list
                resultRuleAction = resultRuleActionList.Where(r => r.emailTemplateId == ruleAction.emailTemplateId).FirstOrDefault();
                isNewObejct = false;
                if (resultRuleAction == null)
                {
                    resultRuleAction = new RuleAction();
                    isNewObejct = true;
                }

                
                resultRuleAction.LetterRequired = ruleAction.LetterRequired;
                
                #region Email
                if (ruleAction.sendEmail)
                {
                    
                    resultRuleAction.sendEmail = true;

                    resultRuleAction.toEmailHashSet = customerExt.AddListSet(resultRuleAction.toEmailHashSet, ruleAction.toEmailHashSet);
                    resultRuleAction.ccEmailHashSet = customerExt.AddListSet(resultRuleAction.ccEmailHashSet, ruleAction.ccEmailHashSet);
                    resultRuleAction.bccEmailHashSet = customerExt.AddListSet(resultRuleAction.bccEmailHashSet, ruleAction.bccEmailHashSet);
                    resultRuleAction.attachmentHashSet = customerExt.AddListSet(resultRuleAction.attachmentHashSet, ruleAction.attachmentHashSet);

                    if (string.IsNullOrEmpty(resultRuleAction.fromEmailId) && !string.IsNullOrEmpty(ruleAction.fromEmailId))
                        resultRuleAction.fromEmailId = ruleAction.fromEmailId;

                    if (ruleAction.emailTemplateId > 0)
                        resultRuleAction.emailTemplateId = ruleAction.emailTemplateId;

                    if (ruleAction.letterId > 0)
                        resultRuleAction.letterId = ruleAction.letterId;

                    resultRuleActionList.Add(resultRuleAction);
                }
                else
                {
                    //except email mention in sendEmail = false

                    customerExt.ExceptWithNullCheck(resultRuleAction.toEmailHashSet, ruleAction.toEmailHashSet);
                    customerExt.ExceptWithNullCheck(resultRuleAction.ccEmailHashSet, ruleAction.ccEmailHashSet);
                    customerExt.ExceptWithNullCheck(resultRuleAction.bccEmailHashSet, ruleAction.bccEmailHashSet);
                    customerExt.ExceptWithNullCheck(resultRuleAction.attachmentHashSet, ruleAction.attachmentHashSet);
                }
                //if (isNewObejct)
                //    resultRuleActionList.Add(resultRuleAction);
                #endregion Email

                #region SMS
                if (ruleAction.sendSMS)
                {
                    //Is the action with emailTemplate already there in finalActionList or else create the object and than add in list
                    resultRuleAction = resultRuleActionList.Where(r => r.smsTemplateId == ruleAction.smsTemplateId).FirstOrDefault();
                    isNewObejct = false;
                    if (resultRuleAction == null)
                    {
                        resultRuleAction = new RuleAction();
                        isNewObejct = true;
                    }

                    resultRuleAction.sendSMS = true;

                    resultRuleAction.contactNoHashSet = customerExt.AddListSet(resultRuleAction.contactNoHashSet, ruleAction.contactNoHashSet);
                    if (ruleAction.smsTemplateId > 0)
                        resultRuleAction.smsTemplateId = ruleAction.smsTemplateId;

                    
                }
                else
                {
                    if (resultRuleAction.contactNoHashSet != null && ruleAction.contactNoHashSet != null)
                        customerExt.ExceptWithNullCheck(resultRuleAction.contactNoHashSet, ruleAction.contactNoHashSet);
                }

                if (isNewObejct)
                    resultRuleActionList.Add(resultRuleAction);

                #endregion SMS
            }
        }

        

        private List<RuleSet> GetRuleSetsByCorpSchemaIdAndToState(Request request)
        {
            List<RuleSet> corporatStateRuleSet = null;
            CorporateRules corporateRule = new CorporateRules();

            //getting the rule for Corporate or Scheme
            if (corporateRules != null && corporateRules.Count>0)
            {
                var query = corporateRules;

                HashSet<CorporateRules> RuleIntersectList = new HashSet<CorporateRules>();
                HashSet<HashSet<CorporateRules>> RuleList = new HashSet<HashSet<CorporateRules>>();
                RuleList.Add(new HashSet<CorporateRules>(query.Where(r => r.corpId == request.CorpId).ToList()));
                RuleList.Add(new HashSet<CorporateRules>(query.Where(r => r.insCompID == request.InsCompId).ToList()));
                RuleList.Add(new HashSet<CorporateRules>(query.Where(r => r.schemeId == request.SchemeId).ToList()));
                RuleList.Add(new HashSet<CorporateRules>(query.Where(r => r.dbType == request.DBType).ToList()));
                RuleList.Add(new HashSet<CorporateRules>(query.Where(r => r.toState == request.ToState).ToList()));

                foreach (var rule in RuleList)
                {
                    if(rule != null && rule.Count > 0)
                    {
                        if (RuleIntersectList.Count == 0)
                            RuleIntersectList.AddRange(rule);
                        else
                        {
                            RuleIntersectList.IntersectWith(rule);
                        }
                    }
                }

                corporateRule = RuleIntersectList.FirstOrDefault();

                

                #region "filter Rule"
                ////If DbType is not there in json rule config than ignore the DBType by set request.DBtype=null
                //if (query != null && query.Count > 0)
                //{
                //    query = query.Where(r => r.dbType != null).ToList();
                //    if (query == null || query.Count == 0)
                //        request.DBType = null;

                //}

                //query = query.Where(r => r.corpId == request.CorpId && r.insCompID == request.InsCompId && r.schemeId==request.SchemeId && r.dbType==request.DBType && r.toState==request.ToState ).ToList();

                //if (query == null || query.Count == 0)
                //{
                //    query = corporateRules;
                //    query = query.Where(r => (r.corpId == 0 || r.corpId == null) && r.insCompID == request.InsCompId && r.schemeId == request.SchemeId && r.dbType == request.DBType && r.toState == request.ToState).ToList();
                //}

                //if (query == null || query.Count == 0)
                //{
                //    query = corporateRules;
                //    query = query.Where(r => (r.corpId == 0 || r.corpId == null) && (r.insCompID == 0 || r.insCompID == null) && r.schemeId == request.SchemeId && r.dbType == request.DBType && r.toState == request.ToState).ToList();
                //}

                //if (query == null || query.Count == 0)
                //{
                //    query = corporateRules;
                //    query = query.Where(r => (r.corpId == 0 || r.corpId == null) && (r.insCompID == 0 || r.insCompID == null) && (r.schemeId == 0 || r.schemeId == null) && r.dbType == request.DBType && r.toState == request.ToState).ToList();
                //}

                //if (query == null || query.Count == 0)
                //{
                //    query = corporateRules;
                //    query = query.Where(r => (r.corpId == 0 || r.corpId == null) && (r.insCompID == 0 || r.insCompID == null) && (r.schemeId == 0 || r.schemeId == null) && (r.dbType == null || r.dbType == string.Empty) && r.toState == request.ToState).ToList();
                //}

                //if (query == null || query.Count == 0)
                //{
                //    query = corporateRules;
                //    query = query.Where(r => (r.corpId == 0 || r.corpId == null) && (r.insCompID == 0 || r.insCompID == null) && (r.schemeId == 0 || r.schemeId == null) && (r.dbType == null || r.dbType == string.Empty) && (r.toState == 0 || r.toState == null)).ToList();
                //}
                //corporateRule = query.FirstOrDefault();
                #endregion "filter Rule"
                

            }

            //get the rules of corporate for the specific ToState of Claim
            if (corporateRule != null && corporateRule.ruleSets != null && corporateRule.ruleSets.Count > 0)
            {
                corporatStateRuleSet = new List<RuleSet>();

                corporatStateRuleSet = corporateRule.ruleSets.Where(corpRule => corpRule.filter.toState == request.ToState).ToList();
                return corporatStateRuleSet;
            }

            return corporatStateRuleSet;
        }

        private void FetchRuleFromDB()
        {
            if (_LogFactory != null)
                _LogFactory.GetLogger("Global").Info("RULE_ENGINE: Call DB to get the Rules ");

            //if CommunicationRules list is null than fetch all else fetch only those which are updated recently
            if (communicationRules.Count == 0)
                communicationRules = DBUtility.GetRules();
            else
            {
                var ListOfRuleModifiedAfterCutOff = DBUtility.GetRules(cutOffDateTime);

                if (ListOfRuleModifiedAfterCutOff != null && ListOfRuleModifiedAfterCutOff.Count > 0)
                {
                    //update only those which are modified after Last CutOffTime
                    foreach(var rule in ListOfRuleModifiedAfterCutOff)
                    {
                        var query = communicationRules;

                        query = query.Where(r => r.CorpId == rule.CorpId && r.InsCompId == rule.InsCompId && r.SchemeId == rule.SchemeId && r.DBType == rule.DBType && r.ToState == rule.ToState).ToList();

                        if (query == null || query.Count == 0)
                        {
                            query = communicationRules;
                            query = query.Where(r => (r.CorpId == 0 || r.CorpId == null) && r.InsCompId == rule.InsCompId && r.SchemeId == rule.SchemeId && r.DBType == rule.DBType && r.ToState == rule.ToState).ToList();
                        }

                        if (query == null || query.Count == 0)
                        {
                            query = communicationRules;
                            query = query.Where(r => (r.CorpId == 0 || r.CorpId == null) && (r.InsCompId == 0 || r.InsCompId == null) && r.SchemeId == rule.SchemeId && r.DBType == rule.DBType && r.ToState == rule.ToState).ToList();
                        }

                        if (query == null || query.Count == 0)
                        {
                            query = communicationRules;
                            query = query.Where(r => (r.CorpId == 0 || r.CorpId == null) && (r.InsCompId == 0 || r.InsCompId == null) && (r.SchemeId == 0 || r.SchemeId == null) && r.DBType == rule.DBType && r.ToState == rule.ToState).ToList();
                        }

                        if (query == null || query.Count == 0)
                        {
                            query = communicationRules;
                            query = query.Where(r => (r.CorpId == 0 || r.CorpId == null) && (r.InsCompId == 0 || r.InsCompId == null) && (r.SchemeId == 0 || r.SchemeId == null) && (r.DBType == null || r.DBType == string.Empty) && r.ToState == rule.ToState).ToList();
                        }

                        if (query == null || query.Count == 0)
                        {
                            query = communicationRules;
                            query = query.Where(r => (r.CorpId == 0 || r.CorpId == null) && (r.InsCompId == 0 || r.InsCompId == null) && (r.SchemeId == 0 || r.SchemeId == null) && (r.DBType == null || r.DBType == string.Empty) && (r.ToState == 0 || r.ToState == null)).ToList();
                        }
                        
                        if (query == null || query.Count == 0)
                            query.Select(r=>r.Rules = rule.Rules);
                        
                        if (rule.CreatedOrModifiedDate > cutOffDateTime)
                            cutOffDateTime = rule.CreatedOrModifiedDate;
                    }
                }
            }

            if (_LogFactory != null)
                _LogFactory.GetLogger("Global").Info("RULE_ENGINE: Successfully Get Rules from DB ");
        }

        private void prepareVariables()
        {
            FetchRuleFromDB();
            corporateRules.AddRange(GetRuleObjectfromJson(communicationRules));
            Task.Factory.StartNew(() => GetUpdatedCorporateRule());
        }

        private void GetUpdatedCorporateRule()
        {
            while (true)
            {
                try
                {
                    //cutOffDateTime = System.DateTime.Now;
                    FetchRuleFromDB();

                    corporateRules = GetRuleObjectfromJson(communicationRules);

                    if (_LogFactory != null)
                        _LogFactory.GetLogger("Global").Info("RULE_ENGINE: Get the updated rules and assign to Rule Engine");
                }
                catch (Exception ex)
                {
                    if (_LogFactory != null)
                        _LogFactory.GetLogger("Global").Error("RULE_ENGINE: Failed to get updated Rules due to exception", ex);
                }
                finally
                {
                    try
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(ThreadSleepTimeInMS));
                    }
                    catch { }
                }
            }
        }

        #region Utility

        private string RemoveDuplicate(string inputCSV)
        {
            string distinctResponseCSV;
            if (!string.IsNullOrEmpty(inputCSV))
                distinctResponseCSV = string.Join(",",
                    inputCSV.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries).Distinct());
            else
                distinctResponseCSV = inputCSV;

            return distinctResponseCSV;

        }

        private string GetCSVAppend(string inputCSV, string appendTo)
        {
            string responseCSV = appendTo;

            if (!string.IsNullOrEmpty(inputCSV) && !string.IsNullOrEmpty(responseCSV))
                responseCSV = responseCSV + "," + inputCSV;
            else
                responseCSV = inputCSV;

            return responseCSV;
        }

        private string RemoveFromCSV(string rejectCSV, string removefromCSV)
        {
            string responseCSV = removefromCSV;
            if (!string.IsNullOrEmpty(removefromCSV) && !string.IsNullOrEmpty(rejectCSV))
            {
                var rejectList = rejectCSV.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                responseCSV = string.Join(",", removefromCSV.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries).Except(rejectList));
            }

            return responseCSV;
        }

        private List<CorporateRules> GetRuleObjectfromJson(List<CommunicationRules> communicationRules)
        {
            List<CorporateRules> objList = new List<CorporateRules>();
            if (communicationRules != null && communicationRules.Count > 0)
            {
                foreach (var commRule in communicationRules)
                {
                    objList.Add(GetRuleObjectfromJson(commRule.Rules));
                }
            }

            return objList;
        }

        private CorporateRules GetRuleObjectfromJson(string jsonRule)
        {
            CorporateRules obj = new CorporateRules();
            try
            {
                obj = (CorporateRules)Newtonsoft.Json.JsonConvert.DeserializeObject(jsonRule, typeof(CorporateRules));
                if (_LogFactory != null)
                    _LogFactory.GetLogger("Global").Info("RULE_ENGINE: Converted Json to Object");

            }
            catch (Exception ex)
            {
                if (_LogFactory != null)
                    _LogFactory.GetLogger("Global").Error("RULE_ENGINE: Failed to convert Json Rule to CorporateRules Object", ex);
            }


            return obj;
        }

        #endregion
    }

    public class CustomExtension
    {
        public List<string> AddListSet(List<string> addToSet, List<string> valueset)
        {
            if (addToSet != null)
            {
                if (valueset != null)
                {
                    //remove the value start with "!"
                    List<string> removal = valueset.Where(s => s.StartsWith("!")).ToList();

                    if (removal != null)
                    {
                        valueset = ExceptWithNullCheck(valueset, removal);
                        removal = removal.Select(s => s = s.Replace("!", "")).ToList();
                        //first remove from input
                        valueset = ExceptWithNullCheck(valueset, removal);
                        //remove from final set
                        addToSet = ExceptWithNullCheck(addToSet, removal);
                    }

                    addToSet = addToSet.Union(valueset).Distinct().ToList();
                }
            }
            else
            {
                if (valueset!=null)
                    addToSet = valueset.Where(s => !s.StartsWith("!")).ToList();
            }

            return addToSet;
        }

        public List<string> ExceptWithNullCheck(List<string> sourceHashSet, List<string> excepSet)
        {
            if (sourceHashSet != null && excepSet != null)
                sourceHashSet.Except(excepSet);

            return sourceHashSet;
        }

        public List<string> ReplaceValue(List<string> sourceHashSet, string newValue, string tag)
        {
            if (sourceHashSet != null)
            {
                if (sourceHashSet.Contains(tag))
                {
                    sourceHashSet.Add(newValue);
                    sourceHashSet.Remove(tag);
                }
            }

            return sourceHashSet;
        }

        public List<string> RemoveDuplicate(List<string> inputList)
        {
            List<string> list = new List<string>();
            if (inputList != null && inputList.Count > 0)
            {
                inputList.RemoveAll(s => string.IsNullOrWhiteSpace(s));

                foreach (var item in inputList)
                {
                    if (item != null && item.Contains(","))
                    {
                        list.AddRange(item.Split(',').ToList());
                    }
                }
                
                inputList.RemoveAll(x => x.Contains(","));
                inputList.AddRange(list);
                inputList = inputList.Distinct().ToList();
                
            }
            return inputList;
        }
        
        public T ToEnum<T>(string value)
        {
            return (T)System.Enum.Parse(typeof(T), value);
        }
    }

    public static class Extensions
    {
        public static bool AddRange<T>(this HashSet<T> @this, IEnumerable<T> items)
        {
            bool allAdded = true;
            foreach (T item in items)
            {
                allAdded &= @this.Add(item);
            }
            return allAdded;
        }
    }
}
