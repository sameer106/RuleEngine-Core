using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using RuleEngine.DataObject;
using System.Reflection;

namespace RuleEngine.Core
{
    public class PrecompiledRules
    {
        public List<Func<T, bool>> CompileRule<T>(List<T> targetEntity, List<RuleExpression> rules)
        {
            CustomExtension customExt = new CustomExtension();
            var compiledRules = new List<Func<T, bool>>();
            ExpressionType expression;

            // Loop through the rules and compile them against the properties of the supplied object 
            rules.ForEach(rule =>
            {
                expression = customExt.ToEnum<ExpressionType>(rule.operation.Trim());
                var genericType = Expression.Parameter(typeof(T));
                var key = MemberExpression.Property(genericType, rule.propertyName.Trim());
                var propertyType = typeof(T).GetProperty(rule.propertyName.Trim()).PropertyType;
                var value = Expression.Constant(Convert.ChangeType(rule.value.Trim(), propertyType));
                var binaryExpression = Expression.MakeBinary(expression, key, value);
                
                compiledRules.Add(Expression.Lambda<Func<T,bool>>(binaryExpression, genericType).Compile());
            });

            // Return the compiled rules
            return compiledRules;
        }

        public List<Func<T, bool>> CompileRule<T>(T targetEntity, List<RuleExpression> rules)
        {
            var compiledRules = new List<Func<T, bool>>();
           
            // Loop through the rules and compile them against the properties of the supplied object 
            rules.ForEach(rule =>
            {
                if (rule.operation.ToString().ToUpper().Trim() == "NOTIN")
                {
                    checkNOTINExpression(targetEntity, rule, compiledRules);
                }
                else if(rule.operation.ToString().ToUpper().Trim() == "IN")
                {
                    checkINExpression(targetEntity, rule, compiledRules);
                }
                else
                {
                    checkExpression(targetEntity, rule, compiledRules);
                }
            });

            // Return the compiled rules
            return compiledRules;
        }

        public void checkExpression<T>(T targetEntity, RuleExpression rule, List<Func<T, bool>> compiledRules)
        {
            CustomExtension customExt = new CustomExtension();
            ExpressionType expression;
            ConstantExpression value;
            expression = customExt.ToEnum<ExpressionType>(rule.operation.Trim());
            var genericType = Expression.Parameter(typeof(T));
            var key = MemberExpression.Property(genericType, rule.propertyName.Trim());
            var propertyType = typeof(T).GetProperty(rule.propertyName.Trim()).PropertyType;

            //to handle if value is Equal to null OR not equal to null
            if (rule.value == null)
                value = Expression.Constant(null);
            else
                value = Expression.Constant(ConvertType(rule.value.Trim(), propertyType));

            var binaryExpression = Expression.MakeBinary(expression, key, value);


            compiledRules.Add(Expression.Lambda<Func<T, bool>>(binaryExpression, genericType).Compile());
        }

        public static object ConvertType(string value,Type toType)
        {
            object obj;
            if (toType == typeof(Boolean))
                obj = Convert.ToBoolean(Convert.ToInt16(value));
            else if (toType == typeof(DateTime))
                obj = Convert.ToDateTime(value);
            else
                obj = Convert.ChangeType(value, toType);

            return obj;
        }

        public void checkINExpression<T>(T targetEntity, RuleExpression rule, List<Func<T, bool>> compiledRules)
        {
            var ruleValueList = rule.value.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries).ToList();

            var genericType = Expression.Parameter(typeof(T));
            var val = GetPropValue(targetEntity, rule.propertyName.Trim());
            
            BinaryExpression binaryExpression = null;
            
            //check condition and make binary expression true or false as per condition result
            if (ruleValueList != null && ruleValueList.Count > 0 && ruleValueList.Contains((val?? "").ToString()))
                binaryExpression = Expression.MakeBinary(ExpressionType.Equal, Expression.Constant(1), Expression.Constant(1));
            else
                binaryExpression = Expression.MakeBinary(ExpressionType.Equal, Expression.Constant(1), Expression.Constant(2));

            compiledRules.Add(Expression.Lambda<Func<T, bool>>(binaryExpression, genericType).Compile());
        }

        public void checkNOTINExpression<T>(T targetEntity, RuleExpression rule, List<Func<T, bool>> compiledRules)
        {
            var ruleValueList = rule.value.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries).ToList();

            var genericType = Expression.Parameter(typeof(T));
            var val = GetPropValue(targetEntity, rule.propertyName.Trim());

            BinaryExpression binaryExpression = null;

            //check condition and make binary expression true or false as per condition result
            if (ruleValueList != null && ruleValueList.Count > 0 && !ruleValueList.Contains((val ?? "").ToString()))
                binaryExpression = Expression.MakeBinary(ExpressionType.Equal, Expression.Constant(1), Expression.Constant(1));
            else
                binaryExpression = Expression.MakeBinary(ExpressionType.Equal, Expression.Constant(1), Expression.Constant(2));

            compiledRules.Add(Expression.Lambda<Func<T, bool>>(binaryExpression, genericType).Compile());
        }

        public static object GetPropValue(object src, string propName)
        {
            return src.GetType().GetProperty(propName).GetValue(src, null);
        }
    }
}
