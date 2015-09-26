using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PexMe.Core;
using PexMe.Common;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;

namespace PexMe.PersistentStore
{
    /// <summary>
    /// A persistent version of field store that includes only strings and integers but no classes from 
    /// Pex. This is access just before persisting the information to files and before loading the information back
    /// </summary>
    [Serializable]
    internal class PersistentFieldStore
    {
        /// <summary>
        /// Stores how the field is modified by each method
        /// </summary>
        public System.Collections.Generic.Dictionary<string, FieldModificationType> ModificationTypeDictionary
            = new System.Collections.Generic.Dictionary<string, FieldModificationType>();

        /// <summary>
        /// Stores how the field is modified by each method more precisely
        /// </summary>
        public System.Collections.Generic.Dictionary<string, FieldModificationType> PreciseModificationTypeDictionary
            = new System.Collections.Generic.Dictionary<string, FieldModificationType>();

        /// <summary>
        /// Name of the field
        /// </summary>
        public string FieldName;

        /// <summary>
        /// Set of concrete values assigned to this field
        /// </summary>
        public HashSet<string> FieldValues = new HashSet<string>();

        /// <summary>
        /// Methods that use this field. Each entry of the method is of the form "assemblyname#typename#methodsignature"
        /// </summary>
        public HashSet<string> ReadMethods = new HashSet<string>();

        /// <summary>
        /// Methods that modify this field from different classes. The key is the "assemblyname#typename", and each entry is of the form "assemblyname#typename#methodsignature"
        /// </summary>
        public System.Collections.Generic.Dictionary<string, HashSet<string>> WriteMethods
            = new System.Collections.Generic.Dictionary<string, HashSet<string>>();

        /// <summary>
        /// Function the retrieves associated Persistent store for each MethodStore
        /// </summary>
        /// <param name="ms"></param>
        /// <param name="pms"></param>
        /// <returns></returns>
        public static bool TryGetPersistentFieldStore(IPexComponent host, FieldStore fs, out PersistentFieldStore pfs)
        {
            pfs = new PersistentFieldStore();

            pfs.FieldName = MethodOrFieldAnalyzer.GetPersistentStringFormOfField(fs.FieldName);
            //foreach(var value in fs.FieldValues)
            //    pfs.FieldValues.Add(value);

            //TODO: Performance can be improved via caching over here
            foreach (var m in fs.ReadMethods)
                pfs.ReadMethods.Add(MethodOrFieldAnalyzer.GetPersistentStringFormOfMethod(m));
            
            foreach (var typeex in fs.WriteMethods.Keys)
            {
                HashSet<string> wmethods = new HashSet<string>();                
                pfs.WriteMethods.Add(MethodOrFieldAnalyzer.GetPersistentStringFormOfTypeEx(typeex), wmethods);
                
                SafeSet<Method> methods;
                bool bresult = fs.WriteMethods.TryGetValue(typeex, out methods);
                SafeDebug.Assume(bresult, "Failed to get associated set of methods for a type");

                var assemblyname = typeex.Definition.Module.Assembly.Location;
                var typename = typeex.FullName;

                foreach (var m in methods)
                {
                    wmethods.Add(assemblyname + PexMeConstants.PexMePersistenceFormSeparator
                        + typename + PexMeConstants.PexMePersistenceFormSeparator + MethodOrFieldAnalyzer.GetMethodSignature(m));
                }
            }

            foreach (var m in fs.ModificationTypeDictionary.Keys)
            {
                var value = fs.ModificationTypeDictionary[m];
                pfs.ModificationTypeDictionary.Add(MethodOrFieldAnalyzer.GetPersistentStringFormOfMethod(m), value);
            }

            foreach (var m in fs.PreciseModificationTypeDictionary.Keys)
            {
                var value = fs.PreciseModificationTypeDictionary[m];
                pfs.PreciseModificationTypeDictionary.Add(MethodOrFieldAnalyzer.GetPersistentStringFormOfMethod(m), value);
            }
                
            return true;
        }

        /// <summary>
        /// Function that retrieves associated method store 
        /// </summary>
        /// <param name="pms"></param>
        /// <param name="ms"></param>
        /// <returns></returns>
        public static bool TryGetFieldStore(IPexComponent host, PersistentFieldStore pfs, out FieldStore fs)
        {
            fs = new FieldStore();

            bool bresult = MethodOrFieldAnalyzer.TryGetFieldFromPersistentStringForm(host, pfs.FieldName, out fs.FieldName);
            SafeDebug.Assume(bresult, "Failed to get field from the persistent store");

            fs.FieldValues.AddRange(pfs.FieldValues);

            //TODO: Performance can be improved via caching over here
            foreach (var mname in pfs.ReadMethods)
            {
                Method m;
                bresult = MethodOrFieldAnalyzer.TryGetMethodFromPersistentStringForm(host, mname, out m);
                SafeDebug.Assume(bresult, "Failed to get method from persistent string form ");
                fs.ReadMethods.Add(m);
            }

            foreach (var typeex in pfs.WriteMethods.Keys)
            {
                SafeSet<Method> wmethods = new SafeSet<Method>();
                TypeEx typeEx;
                bresult = MethodOrFieldAnalyzer.TryGetTypeExFromPersistentStringForm(host, typeex, out typeEx);
                SafeDebug.Assume(bresult, "Failed to get type from persistent string form " + typeex);

                fs.WriteMethods.Add(typeEx, wmethods);

                HashSet<string> methods;
                bresult = pfs.WriteMethods.TryGetValue(typeex, out methods);
                SafeDebug.Assume(bresult, "Failed to get associated set of methods for a type " + typeex);

                foreach (var m in methods)
                {
                    Method method;
                    bresult = MethodOrFieldAnalyzer.TryGetMethodFromPersistentStringForm(host, m, out method);
                    SafeDebug.Assume(bresult, "Failed to get method from string form " + m);
                    wmethods.Add(method);
                }
            }

            foreach (var m in pfs.ModificationTypeDictionary.Keys)
            {
                var value = pfs.ModificationTypeDictionary[m];
                Method method;
                bresult = MethodOrFieldAnalyzer.TryGetMethodFromPersistentStringForm(host, m, out method);
                SafeDebug.Assume(bresult, "Failed to get method from string form " + m);
                fs.ModificationTypeDictionary.Add(method, value);
            }

            foreach (var m in pfs.PreciseModificationTypeDictionary.Keys)
            {
                var value = pfs.PreciseModificationTypeDictionary[m];
                Method method;
                bresult = MethodOrFieldAnalyzer.TryGetMethodFromPersistentStringForm(host, m, out method);
                SafeDebug.Assume(bresult, "Failed to get method from string form " + m);
                fs.PreciseModificationTypeDictionary.Add(method, value);
            }

            return true;
        }
    }
}
