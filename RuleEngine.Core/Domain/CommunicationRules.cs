using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceStack.DataAnnotations;

namespace RuleEngine.Core.Domain
{
    [Serializable]
    [Alias("tblcommunicationrules")]
    public class CommunicationRules
    {
        [AutoIncrement]
        public int CommunicationRuleId { get; set; }
        public int CorpId { get; set; }
        public int InsCompId { get; set; }
        public int SchemeId { get; set; }
        public string Rules { get; set; }
        public DateTime CreatedOrModifiedDate { get; set; }
        public string DBType { get; set; }
        public int ToState { get; set; }
    }
}
