using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PexMe.Core;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using PexMe.Common;
using Microsoft.Pex.Engine.ComponentModel;

namespace PexMe.PersistentStore
{
    /// <summary>
    /// A persistent version of the class MethodStore that is accessed only method store has to be persisted
    /// and loaded back
    /// </summary>
    [Serializable]
    internal class PersistentMethodStore
    {
        /// <summary>
        /// Name of the method
        /// </summary>
        public string methodName;

        /// <summary>
        /// Field read by the current method
        /// </summary>
        public HashSet<string> ReadFields = new HashSet<string>();

        /// <summary>
        /// Fields written by the current method
        /// </summary>
        public HashSet<string> WriteFields = new HashSet<string>();

        /// <summary>
        /// Methods invoking the current method. grouped as its type
        /// </summary>
        public System.Collections.Generic.Dictionary<string, HashSet<string>> CallingMethods
            = new System.Collections.Generic.Dictionary<string, HashSet<string>>();

        /// <summary>
        /// Methods invoked by the current method
        /// </summary>
        public HashSet<string> CalledMethods = new HashSet<string>();

        
        /// <summary>
        /// Function the retrieves associated Persistent store for each MethodStore
        /// </summary>
        /// <param name="ms"></param>
        /// <param name="pms"></param>
        /// <returns></returns>
        public static bool TryGetPersistentMethodStore(MethodStore ms, out PersistentMethodStore pms)
        {
            pms = new PersistentMethodStore();
            pms.methodName = MethodOrFieldAnalyzer.GetPersistentStringFormOfMethod(ms.methodName);

            //foreach (var field in ms.ReadFields)
            //    pms.ReadFields.Add(MethodOrFieldAnalyzer.GetPersistentStringFormOfField(field));

            foreach (var field in ms.WriteFields)
                pms.WriteFields.Add(MethodOrFieldAnalyzer.GetPersistentStringFormOfField(field));

            //foreach (var typeex in ms.CallingMethods.Keys)
            //{
            //    HashSet<string> wmethods = new HashSet<string>();
            //    pms.CallingMethods.Add(MethodOrFieldAnalyzer.GetPersistentStringFormOfTypeEx(typeex), wmethods);

            //    var methods = ms.CallingMethods[typeex];               
            //    var assemblyname = typeex.Definition.Module.Assembly.Location;
            //    var typename = typeex.FullName;

            //    foreach (var m in methods)
            //    {
            //        wmethods.Add(assemblyname + PexMeConstants.PexMePersistenceFormSeparator
            //            + typename + PexMeConstants.PexMePersistenceFormSeparator 
            //            + MethodOrFieldAnalyzer.GetMethodSignature(m));
            //    }
            //}

            //foreach (var calledMethod in ms.CalledMethods)
            //    pms.CalledMethods.Add(MethodOrFieldAnalyzer.GetPersistentStringFormOfMethod(calledMethod));
            
            return true;
        }

        /// <summary>
        /// Function that retrieves associated method store 
        /// </summary>
        /// <param name="pms"></param>
        /// <param name="ms"></param>
        /// <returns></returns>
        public static bool TryGetMethodStore(IPexComponent host, PersistentMethodStore pms, out MethodStore ms)
        {
            ms = new MethodStore();

            bool bresult = MethodOrFieldAnalyzer.TryGetMethodFromPersistentStringForm(host, pms.methodName, out ms.methodName);
            SafeDebug.Assume(bresult, "Failed to get the method from persistent form " + pms.methodName);

            foreach (var fieldstr in pms.ReadFields)
            {
                Field field;
                bresult = MethodOrFieldAnalyzer.TryGetFieldFromPersistentStringForm(host, fieldstr, out field);
                SafeDebug.Assume(bresult, "Failed to get the field from persistent form " + fieldstr);
                ms.ReadFields.Add(field);
            }

            foreach (var fieldstr in pms.WriteFields)
            {
                Field field;
                bresult = MethodOrFieldAnalyzer.TryGetFieldFromPersistentStringForm(host, fieldstr, out field);
                SafeDebug.Assume(bresult, "Failed to get the field from persistent form " + fieldstr);
                ms.WriteFields.Add(field);
            }

            foreach (var typeexstr in pms.CallingMethods.Keys)
            {
                SafeSet<Method> wmethods = new SafeSet<Method>();
                TypeEx typeEx;
                bresult = MethodOrFieldAnalyzer.TryGetTypeExFromPersistentStringForm(host, typeexstr, out typeEx);
                if (!bresult)
                {
                    //No strict safedebugging cannot be added for calling methods since there
                    //can be several dummy methods from Pex side
                    continue;
                }
                
                ms.CallingMethods.Add(typeEx, wmethods);

                var methods = pms.CallingMethods[typeexstr];
                foreach (var mstr in methods)
                {
                    Method method;
                    bresult = MethodOrFieldAnalyzer.TryGetMethodFromPersistentStringForm(host, mstr, out method);
                    if (!bresult)
                        continue;
                    wmethods.Add(method);
                }
            }

            foreach (var calledMethodStr in pms.CalledMethods)
            {
                Method method;
                bresult = MethodOrFieldAnalyzer.TryGetMethodFromPersistentStringForm(host, calledMethodStr, out method);
                if (!bresult)
                    continue;
                ms.CalledMethods.Add(method);
            }

            return true;
        }
    }
}
