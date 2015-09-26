using System;
using System.Collections.Generic;
using System.Text;
using PexMe.Core;
using System.IO;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.ExtendedReflection.Logging;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Collections;
using PexMe.Common;
using Microsoft.Pex.Engine.ComponentModel;

namespace PexMe.PersistentStore
{
    /// <summary>
    /// Class that handles operations for writing to persistent store
    /// </summary>
    public class PexMeDumpWriter
    {
        private IPexComponent host;

        public PexMeDumpWriter(IPexComponent host)
        {
            this.host = host;
        }

        /// <summary>
        /// Dumps entire database into different persistent stores (files)
        /// </summary>
        /// <param name="pmd"></param>
        public void DumpDynamicDatabase(PexMeDynamicDatabase pmd)
        {
            if (!Directory.Exists(PexMeConstants.PexMeStorageDirectory))
                Directory.CreateDirectory(PexMeConstants.PexMeStorageDirectory);

            //Dumping only the contents of factory suggestion store for time being
            //as the classes in extended reflection are not serializable
            try
            {
                DumpFactorySuggestionStore(pmd);
            }
            catch (Exception ex)
            {
                pmd.Log.LogErrorFromException(ex, WikiTopics.MissingWikiTopic, "dumpwriter",
                    "Failed to dump dynamic factory suggestion store");
            }

            if (PexMeConstants.ENABLE_DYNAMICDB_STORAGE)
            {
                //Dump the dynamic field store that includes information of which method modify which fields
                try
                {
                    var filename = Path.Combine(PexMeConstants.PexMeStorageDirectory, PexMeConstants.PexMeDynamicFieldStore);
                    Stream streamWrite = File.Create(filename);
                    BinaryFormatter binaryWrite = new BinaryFormatter();
                    var persistentFieldDic = this.GetPersistentFieldDictionary(pmd.FieldDictionary);
                    binaryWrite.Serialize(streamWrite, persistentFieldDic);
                    streamWrite.Close();
                }
                catch (Exception ex)
                {
                    pmd.Log.LogErrorFromException(ex, WikiTopics.MissingWikiTopic, "dumpwriter",
                        "Failed to dump dynamic field store");
                }
            }

            if (PexMeConstants.ENABLE_DYNAMICDB_STORAGE)
            {
                //Dump the dynamic method store that includes information of which method calls other methods and field they modify
                try
                {
                    var filename = Path.Combine(PexMeConstants.PexMeStorageDirectory, PexMeConstants.PexMeDynamicMethodStore);
                    Stream streamWrite = File.Create(filename);
                    BinaryFormatter binaryWrite = new BinaryFormatter();
                    var persistentMethodDic = this.GetPersistentMethodDictionary(pmd.MethodDictionary);
                    binaryWrite.Serialize(streamWrite, persistentMethodDic);
                    streamWrite.Close();
                }
                catch (Exception ex)
                {
                    pmd.Log.LogErrorFromException(ex, WikiTopics.MissingWikiTopic, "dumpwriter",
                        "Failed to dynamic dump method store");
                }
            }

            //Dump the all explored methods 
            try
            {
                var filename = Path.Combine(PexMeConstants.PexMeStorageDirectory, PexMeConstants.PexMeDynamicExploredMethods);
                Stream streamWrite = File.Create(filename);
                BinaryFormatter binaryWrite = new BinaryFormatter();
                binaryWrite.Serialize(streamWrite, pmd.AllExploredMethods);
                streamWrite.Close();
            }
            catch (Exception ex)
            {
                pmd.Log.LogErrorFromException(ex, WikiTopics.MissingWikiTopic, "dumpwriter",
                    "Failed to dump all explored methods");
            }

            //Dump the all explored methods 
            try
            {
                var filename = Path.Combine(PexMeConstants.PexMeStorageDirectory, PexMeConstants.PexMePendingExplorationMethods);
                Stream streamWrite = File.Create(filename);
                BinaryFormatter binaryWrite = new BinaryFormatter();
                binaryWrite.Serialize(streamWrite, pmd.PendingExplorationMethods);
                streamWrite.Close();
            }
            catch (Exception ex)
            {
                pmd.Log.LogErrorFromException(ex, WikiTopics.MissingWikiTopic, "dumpwriter",
                    "Failed to dump pending explored methods");
            }

            /******************** BEGIN OF DEBUGGING INFO ***********************/
            //Writing monitored methods
            if (PexMeConstants.ENABLE_DYNAMICDB_STORAGE)
            {
                var monitoredMethods = pmd.MonitoredMethods;
                var monitoredMethodsFileName = Path.Combine(PexMeConstants.PexMeStorageDirectory, pmd.AssemblyName + ".dynamic.monitoredmethods.txt");
                using (StreamWriter sw = new StreamWriter(monitoredMethodsFileName))
                {
                    //Printing all monitored methods
                    sw.WriteLine("All monitored methods");
                    foreach (var methodName in pmd.MonitoredMethods)
                        sw.WriteLine(methodName);

                    //Printing all controllable types
                    sw.WriteLine();
                    sw.WriteLine("Controllable types");
                    foreach (var typeName in pmd.ControllableTypes)
                        sw.WriteLine(typeName);

                    //Printing all types for which factory methods are requested
                    sw.WriteLine();
                    sw.WriteLine("Factory requested types");
                    foreach (var typeName in pmd.PexGeneratedFactories.Values)
                        sw.WriteLine(typeName);
                }
            }

            //Writing method information
            if (PexMeConstants.ENABLE_DYNAMICDB_STORAGE)
            {
                var methodDic = pmd.MethodDictionary;
                var methodAccessFileName = Path.Combine(PexMeConstants.PexMeStorageDirectory, pmd.AssemblyName + ".dynamic.methodAccess.txt");
                using (StreamWriter sw = new StreamWriter(methodAccessFileName))
                {
                    foreach (var methodEntry in methodDic.Values)
                    {
                        //Printing write fields
                        sw.WriteLine("Methodname: " + methodEntry.methodName);

                        if (methodEntry.WriteFields.Count > 0)
                        {
                            sw.WriteLine("Write fields: ");
                            foreach (var writeField in methodEntry.WriteFields)
                                sw.WriteLine("\tField: " + writeField);
                        }

                        if (methodEntry.CalledMethods.Count > 0)
                        {
                            sw.WriteLine("Called Methods: ");
                            foreach (var calledMethod in methodEntry.CalledMethods)
                                sw.WriteLine("\t" + calledMethod);
                        }

                        if (methodEntry.CallingMethods.Count > 0)
                        {
                            sw.WriteLine("Calling Methods: ");
                            foreach (var callingMethod in methodEntry.CallingMethods)
                                sw.WriteLine("\t" + callingMethod);
                        }

                        //TODO: Print read fields
                    }
                }
            }

            //Writing field information
            if (PexMeConstants.ENABLE_DYNAMICDB_STORAGE)
            {
                var fieldDic = pmd.FieldDictionary;
                var fieldAccessFileName = Path.Combine(PexMeConstants.PexMeStorageDirectory, pmd.AssemblyName + ".dynamic.fieldAccess.txt");
                using (StreamWriter sw = new StreamWriter(fieldAccessFileName))
                {
                    foreach (var fieldEntry in fieldDic.Values)
                    {
                        //Printing write methods
                        sw.WriteLine("Fieldname: " + fieldEntry.FieldName);
                        sw.WriteLine("Write methods: ");
                        foreach (var writeMethodSet in fieldEntry.WriteMethods.Values)
                        {
                            foreach (var writeMethod in writeMethodSet)
                                sw.WriteLine("\tMethod: " + writeMethod + ", ModificationType: " + FieldStore.GetModificationType(fieldEntry, writeMethod)
                                    + " PreciseModificationType: " + FieldStore.GetPreciseModificationType(fieldEntry, writeMethod));
                        }

                        sw.WriteLine("Field values: ");
                        foreach (var fieldValue in fieldEntry.FieldValues)
                        {
                            sw.WriteLine("\tValue: " + fieldValue);
                        }

                        //TODO: Print read methods
                    }
                }
            }

            //Writing uncovered code locations information
            var uncoveredCLDic = pmd.UncoveredLocationDictionary;
            var uncoveredCLFileName = Path.Combine(PexMeConstants.PexMeStorageDirectory, pmd.AssemblyName + ".uncoveredloc.txt");
            using (StreamWriter sw = new StreamWriter(uncoveredCLFileName))
            {
                foreach (var ucstorelist in uncoveredCLDic.Values)
                {
                    var ucstore = ucstorelist.StoreList[0];                                        
                    sw.WriteLine("CodeLocation: " + ucstore.Location);
                    sw.WriteLine("Relevant Fields: ");
                    //Writing associated fields
                    foreach (var field in ucstore.AllFields)
                        sw.WriteLine("\t" + field);

                    sw.WriteLine("Code Locations and Associated conditions: ");
                    sw.WriteLine("==========================================");
                    //Writing associated terms
                    foreach (var term in ucstore.TextualTerms)
                        sw.WriteLine(term);
                    sw.WriteLine("==========================================");

                    sw.WriteLine("Suggested target method for covering the branch location: ");
                    sw.WriteLine("==========================================================");
                    sw.WriteLine(ucstore.SuggestedMethodsforFactory);
                    sw.WriteLine("==========================================================");                    
                }

                sw.WriteLine("Generted factory methods for this type: ");
                sw.WriteLine("========================================");
                foreach (var facMethodList in pmd.PexGeneratedFactories.Values)
                {                   
                    foreach (string factoryMethod in facMethodList)
                    {
                        sw.WriteLine(factoryMethod);
                    }
                }
            }          
            
            //Dumping the contents of factory suggestion store            
            var fssdebugstore = Path.Combine(PexMeConstants.PexMeStorageDirectory, pmd.AssemblyName + ".fssdebug.txt");
            using (StreamWriter sw = new StreamWriter(fssdebugstore))
            {
                foreach (var fss in pmd.FactorySuggestionsDictionary.Values)
                {
                    sw.WriteLine("==================================================");
                    sw.WriteLine("Records of explorable type: \"" + fss.DeclaringType + "\"");
                    foreach (var codelockey in fss.locationStoreSpecificSequences.Keys)
                    {
                        sw.WriteLine("Key: \"" + codelockey + "\"");
                        var pucls = fss.locationStoreSpecificSequences[codelockey];
                        sw.WriteLine("Dormant status: " + pucls.IsDormat());
                        if (pucls.IsDormat())
                            sw.WriteLine("Associated PUT: " + pucls.AssociatedPUTName);
                        sw.WriteLine("Suggested Sequences........");
                        foreach (var seq in pucls.SuggestedMethodSequences)
                        {
                            sw.WriteLine(seq);
                            sw.WriteLine();
                        }
                    }

                    sw.WriteLine("==================================================");
                    sw.WriteLine("Final suggested sequences");
                    foreach (var mkey in fss.FinalSuggestedMethodSequences.Keys)
                    {
                        sw.WriteLine("Suggested sequences for the method " + mkey + ":");
                        var value = fss.FinalSuggestedMethodSequences[mkey];
                        sw.WriteLine(value.ToString());
                        sw.WriteLine();
                    }

                    sw.WriteLine("==================================================");
                    sw.WriteLine("PUT specific sequences");
                    foreach (var mkey in fss.FinalPUTSequences.Keys)
                    {
                        sw.WriteLine("Suggested sequences for the PUT " + mkey + ":");
                        var value = fss.FinalPUTSequences[mkey];
                        sw.WriteLine(value.ToString());
                        sw.WriteLine();
                    }

                    sw.WriteLine("==================================================");
                    sw.WriteLine("Defect detecting sequences");
                    foreach (var seq in fss.DefectDetectingSequences)
                    {
                        sw.WriteLine(seq);
                    }

                    sw.WriteLine("==================================================");
                    sw.WriteLine("All given up locations (Permanent)");
                    foreach (var loc in fss.PermanentFailedUncoveredLocations)
                    {
                        sw.Write(loc.ToString());
                        if (fss.UncoveredSystemLibLocations.Contains(loc))
                            sw.Write(" ( SystemLib )");
                        sw.WriteLine();
                    }

                    sw.WriteLine("All given up locations (Temporary)");
                    foreach (var loc in fss.TemporaryFailedUncoveredLocations.Keys)
                    {
                        sw.WriteLine(loc.ToString() + ", Attempt: " + fss.TemporaryFailedUncoveredLocations[loc]);
                    }

                    sw.WriteLine("==================================================");
                    sw.WriteLine("All successful locations");
                    foreach (var loc in fss.SuccessfulCoveredLocations)
                    {
                        sw.WriteLine(loc.ToString());
                    }
                }
            }
            /******************** END OF DEBUGGING INFO ***********************/
        }

        public static void DumpFactorySuggestionStore(PexMeDynamicDatabase pmd)
        {
            var filename = Path.Combine(PexMeConstants.PexMeStorageDirectory, PexMeConstants.PexMeFactorySuggestionStore);
            Stream streamWrite = File.Create(filename);
            BinaryFormatter binaryWrite = new BinaryFormatter();
            binaryWrite.Serialize(streamWrite, pmd.FactorySuggestionsDictionary);
            streamWrite.Close();
        }

        private System.Collections.Generic.Dictionary<string, PersistentFieldStore> 
            GetPersistentFieldDictionary(SafeDictionary<Field, FieldStore> fielddic)
        {
            var pfielddic = new System.Collections.Generic.Dictionary<string, PersistentFieldStore>();
            foreach (var field in fielddic.Keys)
            {
                var fieldstr = MethodOrFieldAnalyzer.GetPersistentStringFormOfField(field);
                PersistentFieldStore pfs;
                bool bresult = PersistentFieldStore.TryGetPersistentFieldStore(this.host, fielddic[field], out pfs);
                SafeDebug.Assume(bresult, "Failed to get persistent field store!!!");
                pfielddic[fieldstr] = pfs;
            }
            return pfielddic;
        }

        private System.Collections.Generic.Dictionary<string, PersistentMethodStore> 
            GetPersistentMethodDictionary(SafeDictionary<Method, MethodStore> methoddic)
        {
            var pmethoddic = new System.Collections.Generic.Dictionary<string, PersistentMethodStore>();
            foreach (var method in methoddic.Keys)
            {
                var methodstr = MethodOrFieldAnalyzer.GetPersistentStringFormOfMethod(method);
                PersistentMethodStore pms;
                bool bresult = PersistentMethodStore.TryGetPersistentMethodStore(methoddic[method], out pms);
                SafeDebug.Assume(bresult, "Failed to get persistent method store!!!");
                pmethoddic[methodstr] = pms;
            }

            return pmethoddic;
        }

        /// <summary>
        /// Dumps static database
        /// </summary>
        public void DumpStaticDatabase(PexMeStaticDatabase psd)
        {
            SafeDebug.AssumeNotNull(psd, "psd");

            if (!Directory.Exists(PexMeConstants.PexMeStorageDirectory))
                Directory.CreateDirectory(PexMeConstants.PexMeStorageDirectory);

            //Dump the dynamic field store that includes information of which method modify which fields
            try
            {
                var filename = Path.Combine(PexMeConstants.PexMeStorageDirectory, PexMeConstants.PexMeStaticFieldStore);
                Stream streamWrite = File.Create(filename);
                BinaryFormatter binaryWrite = new BinaryFormatter();
                var persistentFieldDic = this.GetPersistentFieldDictionary(psd.FieldDictionary);
                binaryWrite.Serialize(streamWrite, persistentFieldDic);
                streamWrite.Close();
            }
            catch (Exception ex)
            {
                psd.Log.LogErrorFromException(ex, WikiTopics.MissingWikiTopic, "dumpwriter",
                    "Failed to dump field store of static database");
            }

            //Dump the dynamic method store that includes information of which method calls other methods and field they modify
            try
            {
                var filename = Path.Combine(PexMeConstants.PexMeStorageDirectory, PexMeConstants.PexMeStaticMethodStore);
                Stream streamWrite = File.Create(filename);
                BinaryFormatter binaryWrite = new BinaryFormatter();
                var persistentMethodDic = this.GetPersistentMethodDictionary(psd.MethodDictionary);
                binaryWrite.Serialize(streamWrite, persistentMethodDic);
                streamWrite.Close();
            }
            catch (Exception ex)
            {
                psd.Log.LogErrorFromException(ex, WikiTopics.MissingWikiTopic, "dumpwriter",
                    "Failed to dump method store of static database");
            }

            /******************** DEBUGGING INFO *************************/
            //Writing method information
            var methodDic = psd.MethodDictionary;

            var methodAccessFileName = Path.Combine(PexMeConstants.PexMeStorageDirectory, psd.AssemblyName + ".static.methodAccess.txt");
            using (StreamWriter sw = new StreamWriter(methodAccessFileName))
            {
                foreach (var methodEntry in methodDic.Values)
                {
                    //Printing write fields
                    sw.WriteLine("Methodname: " + methodEntry.methodName);

                    if (methodEntry.WriteFields.Count > 0)
                    {
                        sw.WriteLine("Write fields: ");
                        foreach (var writeField in methodEntry.WriteFields)
                            sw.WriteLine("\tField: " + writeField);
                    }

                    if (methodEntry.CalledMethods.Count > 0)
                    {
                        sw.WriteLine("Called Methods: ");
                        foreach (var calledMethod in methodEntry.CalledMethods)
                            sw.WriteLine("\t" + calledMethod);
                    }

                    if (methodEntry.CallingMethods.Count > 0)
                    {
                        sw.WriteLine("Calling Methods: ");
                        foreach (var callingMethod in methodEntry.CallingMethods)
                            sw.WriteLine("\t" + callingMethod);
                    }

                    //TODO: Print read fields
                }
            }

            //Writing field information
            var fieldDic = psd.FieldDictionary;

            var fieldAccessFileName = Path.Combine(PexMeConstants.PexMeStorageDirectory, psd.AssemblyName + ".static.fieldAccess.txt");
            using (StreamWriter sw = new StreamWriter(fieldAccessFileName))
            {
                foreach (var fieldEntry in fieldDic.Values)
                {
                    //Printing write methods
                    sw.WriteLine("Fieldname: " + fieldEntry.FieldName);
                    sw.WriteLine("Write methods: ");
                    foreach (var writeMethodSet in fieldEntry.WriteMethods.Values)
                    {
                        foreach (var writeMethod in writeMethodSet)
                            sw.WriteLine("\tMethod: " + writeMethod + ", ModificationType: " + FieldStore.GetModificationType(fieldEntry, writeMethod));
                    }

                    sw.WriteLine("Field values: ");
                    foreach (var fieldValue in fieldEntry.FieldValues)
                    {
                        sw.WriteLine("\tValue: " + fieldValue);
                    }

                    //TODO: Print read methods
                }
            }
            /******************** DEBUGGING INFO *************************/            
        }
    }
}
