using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Logging;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using Microsoft.Pex.Engine.ComponentModel;

namespace Covana.Analyzer
{
    public class ObjectCreationProblemAnalyzer
    {
        public static StringBuilder ErrorLog = new StringBuilder("ObjectCreationProblemAnalyzer: \n");

        // Find out how many fields require user provided factory methods
        public static bool GetTargetExplorableField(IEnumerable<Field> involvedFields, out Field targetField,
                                                    out TypeEx declaringType, IPexComponent host,out TypeEx targetType)
        {
            targetField = null;
            var allInvolvedFields = new SafeList<Field>();
            allInvolvedFields.AddRange(involvedFields);
            int numFields = allInvolvedFields.Count;
            if (numFields < 1)
            {
                declaringType = null;
                targetType = null;
                return false;
            }
//
//            if (numFields == 1)
//            {
//                targetField = allInvolvedFields[0];
//                if (!MethodOrFieldAnalyzer.TryGetDeclaringTypeDefinition(host, targetField, out declaringType))
//                {
//                    declaringType = null;
//                    return false;
//                }
//                return true;
//            }

//            allInvolvedFields.Reverse();
            TypeEx prevFieldType = null;
            for (int count = 0; count < allInvolvedFields.Count; count++)
            {
                var currentField = allInvolvedFields[count];
//                if (count + 1 < allInvolvedFields.Count)
//                {
//                    prevFieldType = allInvolvedFields[count + 1].Type;
//                }

                if (!MethodOrFieldAnalyzer.TryGetDeclaringTypeDefinition(host, currentField, out declaringType))
                {
                    host.Log.LogError(WikiTopics.MissingWikiTopic, "targetfield",
                                      "Failed to get the declaring type for the field " + currentField.FullName);
                    ErrorLog.AppendLine("Failed to get the declaring type for the field " + currentField.FullName);
                    targetType = null;
                    return false;
                }

                try
                {
                    SafeDebug.Assume(
                        prevFieldType == null || prevFieldType == declaringType ||
                        prevFieldType.IsAssignableTo(declaringType)
                        || declaringType.IsAssignableTo(prevFieldType),
                        "The current field type (" + declaringType + ") should be the same as the previous field type (" +
                        prevFieldType + ")");


                    if (MethodOrFieldAnalyzer.IsFieldExternallyVisible(host, declaringType, currentField))
                    {
                        prevFieldType = currentField.Type;
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    ErrorLog.AppendLine("Failed to compute external visibility of field " + currentField.FullName + " because of " + ex);
                }

                targetField = currentField;
                targetType = declaringType;
                return true;
            }

            targetField = allInvolvedFields[allInvolvedFields.Count - 1];
            if (!MethodOrFieldAnalyzer.TryGetDeclaringTypeDefinition(host, targetField, out declaringType))
            {
                host.Log.LogError(WikiTopics.MissingWikiTopic, "targetfield",
                                  "Failed to get the declaring type for the field " + targetField.FullName);
                targetType = null;
                return false;
            }
            targetType = targetField.Type;
            return true;
        }
    }
}