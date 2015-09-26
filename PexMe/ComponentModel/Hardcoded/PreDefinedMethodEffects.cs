using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.Pex.Engine.ComponentModel;
using PexMe.Core;
using Microsoft.ExtendedReflection.Logging;
using Microsoft.ExtendedReflection.Collections;

namespace PexMe.ComponentModel.Hardcoded
{
    /// <summary>
    /// Includes hardcoded method effects that cannot be computed using existing static analysis
    /// in PexMeStaticDatabase code. 
    /// </summary>
    public class PreDefinedMethodEffects
    {
        /// <summary>
        /// Predefined effects should be given in the specified format. The format is as follows
        /// {"assembly name", "class name", "field name", "format type", "method name"}
        /// DO NOT FORGET TO INCREASE NumRecords after adding new entries to predefinedEffects
        /// </summary>
        internal static int NumRecords = 1;
        internal static string[,] predefinedEffects = { 
                                                      { "mscorlib", "System.Collections.DictionaryBase", "System.Collections.DictionaryBase.hashtable", "NON_NULL_SET", "System.Collections.DictionaryBase.System.Collections.IDictionary.Add(System.Collections.DictionaryBase,System.Object,System.Object)" }
                                                      };
        

        private static bool isPredefinedEffectsParsed = false;
        internal static Dictionary<PreDefinedMethodEffectsStore, PreDefinedMethodEffectsStore> effectsStore = null;
        
        /// <summary>
        /// parses the predefined effects described above
        /// </summary>
        /// <returns></returns>
        public static bool ParsePredefinedEffects(IPexComponent host)
        {
            //already parsed
            if (isPredefinedEffectsParsed)
                return true;

            effectsStore = new Dictionary<PreDefinedMethodEffectsStore, PreDefinedMethodEffectsStore>();

            //parse each record
            int numRecords = NumRecords;
            for (int count = 0; count < numRecords; count++ )
            {
                string assemblyname = predefinedEffects[count, 0];
                string typename = predefinedEffects[count, 1];
                string fieldname = predefinedEffects[count, 2];
                string fmttype = predefinedEffects[count, 3];
                string methodname = predefinedEffects[count, 4];

                ParseRecord(host, assemblyname, typename, fieldname, fmttype, methodname);    
            }

            isPredefinedEffectsParsed = true;
            return true;
        }

        /// <summary>
        /// Parses each record and stores into internal dictionary
        /// </summary>
        /// <param name="assemblyname"></param>
        /// <param name="typename"></param>
        /// <param name="fieldname"></param>
        /// <param name="fmttype"></param>
        /// <param name="methodname"></param>
        private static void ParseRecord(IPexComponent host, string assemblyname, string typename, string fieldname, string fmttype, string methodname)
        {                    
            Field field;
            if(!MethodOrFieldAnalyzer.TryGetField(host, assemblyname, typename, fieldname, out field))
            {
                host.Log.LogWarning(WikiTopics.MissingWikiTopic, "PredefinedEffects", "Failed to load field: " + fieldname);
                return;
            }

            FieldModificationType fmt = FieldStore.GetModificationTypeFromString(fmttype);

            Method method;
            if (!MethodOrFieldAnalyzer.TryGetMethod(host, assemblyname, typename, methodname, out method))
            {
                host.Log.LogWarning(WikiTopics.MissingWikiTopic, "PredefinedEffects", "Failed to load method: " + methodname);
                return;
            }                      
            
            //storing into the effects store
            PreDefinedMethodEffectsStore pdme = new PreDefinedMethodEffectsStore(field, fmt);
            List<Method> suggestedMethods;
            PreDefinedMethodEffectsStore existingPdme;
            if (!effectsStore.TryGetValue(pdme, out existingPdme))
            {
                suggestedMethods = new List<Method>();
                pdme.suggestedmethodList = suggestedMethods;
                effectsStore.Add(pdme, pdme);
            }
            else
            {
                suggestedMethods = existingPdme.suggestedmethodList;
            }

            suggestedMethods.Add(method);
        }

        /// <summary>
        /// Retrieves the write methods for a field, if exists from the predefined settings.
        /// </summary>
        public static bool TryGetWriteMethods(IPexComponent host, Field field, FieldModificationType desiredFmt, 
            out SafeSet<Method> writeMethods)
        {
            if (!isPredefinedEffectsParsed)
                ParsePredefinedEffects(host);

            PreDefinedMethodEffectsStore pdme = new PreDefinedMethodEffectsStore(field, desiredFmt);
            PreDefinedMethodEffectsStore existingPdme;
            if (effectsStore.TryGetValue(pdme, out existingPdme))
            {
                writeMethods = new SafeSet<Method>();
                writeMethods.AddRange(existingPdme.suggestedmethodList);
                return true;
            }
            else
            {
                writeMethods = null;
                return false;
            }
        }
    }
}
