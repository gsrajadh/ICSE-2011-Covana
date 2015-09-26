using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.ExtendedReflection.Logging;
using PexMe.TermHandler;
using PexMe.Core;

namespace PexMe.ComponentModel
{
    /// <summary>
    /// Computes fitness values. This is based on the Xie et al's DSN 2009 paper
    /// </summary>
    internal class FitnessMeasure
    {
        public const int FitnessConstant = 1;

        /// <summary>
        /// Computes fitness values
        /// </summary>
        /// <param name="host"></param>
        /// <param name="binOp"></param>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="bNegated"></param>
        /// <returns></returns>
        public static int ComputeFitnessValue(IPexComponent host, BinaryOperator binOp, int left, int right, bool bNegated)
        {
            int fitnessval = -1;
            
            //TODO: How to handle <= or >= operators. How did they come???

            switch (binOp)
            {
                case BinaryOperator.Ceq:
                    if(!bNegated)
                    {
                        //a == b
                        fitnessval = Math.Abs(left - right);
                    }
                    else
                    {
                        //a != b
                        //TODO: How to define the fitness measure?
                        host.Log.LogWarning(WikiTopics.MissingWikiTopic, "fitness measure",
                            "encountered the condition a != b");
                    }
                    break;
                case BinaryOperator.Clt:
                    if (!bNegated)
                    {
                        //a < b
                        fitnessval = (left - right) + FitnessMeasure.FitnessConstant;
                    }
                    else
                    {
                        //a > b
                        fitnessval = (right - left) + FitnessMeasure.FitnessConstant;
                    }
                    break;
                default:
                    host.Log.LogWarning(WikiTopics.MissingWikiTopic, "fitness measure",
                        "Unknown binary operator. Failed to compute the fitness measure");
                    break;
            }

            return fitnessval;
        }


        /// <summary>
        /// Computes fitness values. TODO: handles only integer and boolean. Needs to be extended for other types
        /// </summary>
        /// <param name="field"></param>
        /// <param name="actual"></param>
        /// <param name="expected"></param>
        /// <param name="host"></param>
        /// <param name="fmt"></param>
        /// <param name="fitnessval"></param>
        public static void ComputeFitnessValue(Field field, FieldValueHolder actual, FieldValueHolder expected, IPexComponent host,
            out FieldModificationType fmt, out int fitnessval)
        {
            fitnessval = Int32.MaxValue;
            fmt = FieldModificationType.UNKNOWN;
            try
            {
                string fieldType = field.Type.ToString();                
                if (fieldType == "System.Int32")
                {
                    fitnessval = Math.Abs(actual.intValue - expected.intValue);
                    if (actual.intValue < expected.intValue)
                    {
                        fmt = FieldModificationType.INCREMENT;
                    }
                    else if (actual.intValue > expected.intValue)
                    {
                        fmt = FieldModificationType.DECREMENT;
                    }
                    return;
                }

                if (fieldType == "System.Boolean")
                {
                    if (expected.boolValue)
                        fmt = FieldModificationType.TRUE_SET;
                    else
                        fmt = FieldModificationType.FALSE_SET;
                    return;
                }

                if (fieldType == "System.Int16")
                {
                    fitnessval = Math.Abs(actual.shortValue - expected.shortValue);
                    if (actual.shortValue < expected.shortValue)
                    {
                        fmt = FieldModificationType.INCREMENT;
                    }
                    else if (actual.shortValue > expected.shortValue)
                    {
                        fmt = FieldModificationType.DECREMENT;
                    }
                    return;
                }

                if (fieldType == "System.Int64")
                {
                    fitnessval = (int)Math.Abs(actual.longValue - expected.longValue);
                    if (actual.longValue < expected.longValue)
                    {
                        fmt = FieldModificationType.INCREMENT;
                    }
                    else if (actual.longValue > expected.longValue)
                    {
                        fmt = FieldModificationType.DECREMENT;
                    }
                    return;
                }

                if (field.Type.IsReferenceType)
                {
                    if (expected.objValue == null)
                        fmt = FieldModificationType.NULL_SET;
                    else
                        fmt = FieldModificationType.NON_NULL_SET;
                }

                host.Log.LogWarning(WikiTopics.MissingWikiTopic, "FitnexCompute", "Handles only integer and boolean types.");
            }
            catch (OverflowException ofe)
            {
                host.Log.LogCriticalFromException(ofe, WikiTopics.MissingWikiTopic, "FitnessMeasure", "Overflow error occurred while computing fitness values");
            }
        }
    }
}
