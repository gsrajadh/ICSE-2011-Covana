using System;
using System.Collections.Generic;
using System.Text;
using PexMe.Core;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.ExtendedReflection.Logging;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using PexMe.ObjectFactoryObserver;
using PexMe.Common;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Collections;

namespace PexMe.PersistentStore
{
    /// <summary>
    /// Class that reads dump files back into persistent store
    /// </summary>
    internal class PexMeDumpReader
    {      
        /// <summary>
        /// Tries to load dynamic database
        /// </summary>        
        /// <param name="pmd"></param>
        /// <returns></returns>
        public static bool TryLoadDynamicDatabase(PexMeDynamicDatabase pmd)
        {
            SafeDebug.AssumeNotNull(pmd, "pmd");

            var suggestionstore = Path.Combine(PexMeConstants.PexMeStorageDirectory, PexMeConstants.PexMeFactorySuggestionStore);
            if (!File.Exists(suggestionstore))
                pmd.FactorySuggestionsDictionary
                    = new System.Collections.Generic.Dictionary<string, PexMe.ObjectFactoryObserver.FactorySuggestionStore>();
            else
            {
                try
                {
                    Stream streamRead = File.OpenRead(suggestionstore);
                    BinaryFormatter binaryRead = new BinaryFormatter();
                    pmd.FactorySuggestionsDictionary = binaryRead.Deserialize(streamRead)
                        as System.Collections.Generic.Dictionary<string, FactorySuggestionStore>;
                    streamRead.Close();                    
                }
                catch (Exception ex)
                {
                    //host.Log.LogErrorFromException(ex, WikiTopics.MissingWikiTopic, "dumpreader",
                    //    "Failed to read the factory suggestion store");
                    pmd.FactorySuggestionsDictionary
                        = new System.Collections.Generic.Dictionary<string, PexMe.ObjectFactoryObserver.FactorySuggestionStore>();                    
                }
            }

            if (PexMeConstants.ENABLE_DYNAMICDB_STORAGE)
            {
                var fieldstore = Path.Combine(PexMeConstants.PexMeStorageDirectory, PexMeConstants.PexMeDynamicFieldStore);
                if (!File.Exists(fieldstore))
                    pmd.FieldDictionary = new SafeDictionary<Field, FieldStore>();
                else
                {
                    try
                    {
                        Stream streamRead = File.OpenRead(fieldstore);
                        BinaryFormatter binaryRead = new BinaryFormatter();
                        System.Collections.Generic.Dictionary<string, PersistentFieldStore> pfs = binaryRead.Deserialize(streamRead)
                            as System.Collections.Generic.Dictionary<string, PersistentFieldStore>;
                        pmd.FieldDictionary = GetFieldDictionary(pmd, pfs);
                        streamRead.Close();
                    }
                    catch (Exception)
                    {
                        //host.Log.LogErrorFromException(ex, WikiTopics.MissingWikiTopic, "dumpreader",
                        //    "Failed to read the dynamic field store");
                        pmd.FieldDictionary = new SafeDictionary<Field, FieldStore>();
                    }
                }
            }
            else
            {
                pmd.FieldDictionary = new SafeDictionary<Field, FieldStore>();
            }

            if (PexMeConstants.ENABLE_DYNAMICDB_STORAGE)
            {
                var methodstore = Path.Combine(PexMeConstants.PexMeStorageDirectory, PexMeConstants.PexMeDynamicMethodStore);
                if (!File.Exists(methodstore))
                    pmd.MethodDictionary = new SafeDictionary<Method, MethodStore>();
                else
                {
                    try
                    {
                        Stream streamRead = File.OpenRead(methodstore);
                        BinaryFormatter binaryRead = new BinaryFormatter();
                        System.Collections.Generic.Dictionary<string, PersistentMethodStore> pfs = binaryRead.Deserialize(streamRead)
                            as System.Collections.Generic.Dictionary<string, PersistentMethodStore>;
                        pmd.MethodDictionary = GetMethodDictionary(pmd, pfs);
                        streamRead.Close();
                    }
                    catch (Exception)
                    {
                        //host.Log.LogErrorFromException(ex, WikiTopics.MissingWikiTopic, "dumpreader",
                        //    "Failed to read the dynamic field store");
                        pmd.MethodDictionary = new SafeDictionary<Method, MethodStore>();
                    }
                }
            }
            else
            {
                pmd.MethodDictionary = new SafeDictionary<Method, MethodStore>();
            }

            var expstore = Path.Combine(PexMeConstants.PexMeStorageDirectory, PexMeConstants.PexMeDynamicExploredMethods);
            if (!File.Exists(expstore))
                pmd.AllExploredMethods = new HashSet<string>();
            else
            {
                try
                {
                    Stream streamRead = File.OpenRead(expstore);
                    BinaryFormatter binaryRead = new BinaryFormatter();
                    pmd.AllExploredMethods = binaryRead.Deserialize(streamRead)
                        as HashSet<string>;                     
                    streamRead.Close();
                }
                catch (Exception)
                {
                    //host.Log.LogErrorFromException(ex, WikiTopics.MissingWikiTopic, "dumpreader",
                    //    "Failed to read the dynamic field store");
                    pmd.AllExploredMethods = new HashSet<string>();
                }
            }

            var pendingstore = Path.Combine(PexMeConstants.PexMeStorageDirectory, PexMeConstants.PexMePendingExplorationMethods);
            if (!File.Exists(pendingstore))
                pmd.PendingExplorationMethods = new HashSet<string>();
            else
            {
                try
                {
                    Stream streamRead = File.OpenRead(pendingstore);
                    BinaryFormatter binaryRead = new BinaryFormatter();
                    pmd.PendingExplorationMethods = binaryRead.Deserialize(streamRead)
                        as HashSet<string>;
                    streamRead.Close();
                }
                catch (Exception)
                {
                    //host.Log.LogErrorFromException(ex, WikiTopics.MissingWikiTopic, "dumpreader",
                    //    "Failed to read the dynamic field store");
                    pmd.PendingExplorationMethods = new HashSet<string>();
                }
            }

            return true;
        }

        private static SafeDictionary<Field, FieldStore> GetFieldDictionary(IPexComponent host, 
            System.Collections.Generic.Dictionary<string, PersistentFieldStore> pfielddic)
        {
            var fielddic = new SafeDictionary<Field, FieldStore>();
            foreach (var fieldstr in pfielddic.Keys)
            {
                Field field;
                bool bresult = MethodOrFieldAnalyzer.TryGetFieldFromPersistentStringForm(host, fieldstr, out field);
                SafeDebug.Assume(bresult, "Failed to get field from persistent store!!!");

                FieldStore fs;
                bresult = PersistentFieldStore.TryGetFieldStore(host, pfielddic[fieldstr], out fs);
                SafeDebug.Assume(bresult, "Failed to get field store!!!");

                fielddic[field] = fs;
            }
            return fielddic;
        }

        private static SafeDictionary<Method, MethodStore> GetMethodDictionary(IPexComponent host, 
            System.Collections.Generic.Dictionary<string, PersistentMethodStore> pmethoddic)
        {
            var methoddic = new SafeDictionary<Method, MethodStore>();
            foreach (var methodstr in pmethoddic.Keys)
            {
                Method method;
                bool bresult = MethodOrFieldAnalyzer.TryGetMethodFromPersistentStringForm(host, methodstr, out method);
                if (!bresult)
                    continue;

                MethodStore ms;
                bresult = PersistentMethodStore.TryGetMethodStore(host, pmethoddic[methodstr], out ms);
                if (!bresult)
                    continue;
                
                //SafeDebug.Assume(bresult, "Failed to get method from persistent store!!!");
                methoddic[method] = ms;
            }

            return methoddic;
        }

        /// <summary>
        /// Tries to load static database
        /// </summary>
        /// <param name="host"></param>
        /// <param name="psd"></param>
        /// <returns></returns>
        public static bool TryLoadStaticDatabase(PexMeStaticDatabase psd)
        {
            var fieldstore = Path.Combine(PexMeConstants.PexMeStorageDirectory, PexMeConstants.PexMeStaticFieldStore);
            if (!File.Exists(fieldstore))
                psd.FieldDictionary = new SafeDictionary<Field, FieldStore>();
            else
            {
                try
                {
                    Stream streamRead = File.OpenRead(fieldstore);
                    if (streamRead.Length <= PexMeConstants.MAX_ALLOWED_STATIC_STORAGE)
                    {
                        BinaryFormatter binaryRead = new BinaryFormatter();
                        System.Collections.Generic.Dictionary<string, PersistentFieldStore> pfs = binaryRead.Deserialize(streamRead)
                            as System.Collections.Generic.Dictionary<string, PersistentFieldStore>;
                        psd.FieldDictionary = GetFieldDictionary(psd, pfs);
                    } 
                    else
                        psd.FieldDictionary = new SafeDictionary<Field, FieldStore>();
                    streamRead.Close();
                }
                catch (Exception)
                {
                    //host.Log.LogErrorFromException(ex, WikiTopics.MissingWikiTopic, "dumpreader",
                    //    "Failed to read the dynamic field store");
                    psd.FieldDictionary = new SafeDictionary<Field, FieldStore>();
                }
            }

            var methodstore = Path.Combine(PexMeConstants.PexMeStorageDirectory, PexMeConstants.PexMeStaticMethodStore);
            if (!File.Exists(methodstore))
                psd.MethodDictionary = new SafeDictionary<Method, MethodStore>();
            else
            {
                try
                {
                    Stream streamRead = File.OpenRead(methodstore);
                    if (streamRead.Length <= PexMeConstants.MAX_ALLOWED_STATIC_STORAGE)
                    {
                        BinaryFormatter binaryRead = new BinaryFormatter();
                        System.Collections.Generic.Dictionary<string, PersistentMethodStore> pfs = binaryRead.Deserialize(streamRead)
                            as System.Collections.Generic.Dictionary<string, PersistentMethodStore>;
                        psd.MethodDictionary = GetMethodDictionary(psd, pfs);
                    }
                    else
                        psd.FieldDictionary = new SafeDictionary<Field, FieldStore>();
                    streamRead.Close();
                }
                catch (Exception)
                {
                    //host.Log.LogErrorFromException(ex, WikiTopics.MissingWikiTopic, "dumpreader",
                    //    "Failed to read the dynamic field store");
                    psd.MethodDictionary = new SafeDictionary<Method, MethodStore>();
                }
            }

            return true;
        }
    }
}
