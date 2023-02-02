using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace RuleEngine.DataObject
{

    public class CorporateRules
    {
        public CorporateRules()
        {
            ruleSets = new List<RuleSet>();
        }

        public int? corpId { get; set; }
        public int? schemeId { get; set; }
        public int? insCompID { get; set; }
        public string dbType { get; set; }
        public int? toState { get; set; }
        public List<RuleSet> ruleSets { get; set; }
    }

    public class RuleSet
    {
        public RuleSet()
        {
            action = new List<RuleAction>();
            filter = new RuleFilter();
        }

        public RuleFilter filter { get; set; }
        public List<RuleAction> action { get; set; }
    }

    public class RuleFilter
    {
        public int fromState { get; set; }
        public int toState { get; set; }
        public List<RuleExpression> ruleExpression { get; set; }
        
    }

    public class RuleExpression
    {
        public RuleExpression(string _propertyName, string _operation ,string _value )
        {
            propertyName = _propertyName;
            operation = _operation;
            value = _value;
        }

        public string propertyName { get; set; }
        public string operation { get; set; }
        public string value { get; set; }
    
    }

    public class RuleAction
    {
        //Email
        public bool sendEmail { get; set; }
        public bool LetterRequired { get; set; }
        public List<string> toEmailHashSet { get; set; }
        public List<string> ccEmailHashSet { get; set; }
        public List<string> bccEmailHashSet { get; set; }
        public string fromEmailId { get; set; }
        public long emailTemplateId { get; set; }
        public List<string> attachmentHashSet { get; set; }
        public long letterId { get; set; }

        //SMS
        public bool sendSMS { get; set; }
        public List<string> contactNoHashSet { get; set; }
        public int smsTemplateId { get; set; }
    }

    
    
    
}
