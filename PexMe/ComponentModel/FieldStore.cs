using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Metadata;

namespace PexMe.Core
{
    ///<summary>
    ///Constants that describe how the field is modified
    ///</summary>
    public enum FieldModificationType { INCREMENT, INCREMENT_ONE, DECREMENT, NON_NULL_SET, NULL_SET, UNKNOWN, TRUE_SET, FALSE_SET, METHOD_CALL };
       
    /// <summary>
    /// Represents a Field (and entire information of the field) to be stored in the database
    /// Same class is used to store information for both static and dynamic databases
    /// </summary>
    [Serializable]
    public class FieldStore
    {
        /// <summary>
        /// Stores how the field is modified by each method
        /// </summary>
        public SafeDictionary<Method, FieldModificationType> ModificationTypeDictionary = new SafeDictionary<Method, FieldModificationType>();

        /// <summary>
        /// Stores how the field is modified by each method, more precise information
        /// of how exactly the modification happens. Should be used only for LOOP_FEATURE
        /// but not for others.
        /// </summary>
        public SafeDictionary<Method, FieldModificationType> PreciseModificationTypeDictionary = new SafeDictionary<Method, FieldModificationType>();
        
        /// <summary>
        /// Name of the field
        /// </summary>
        public Field FieldName;
        
        /// <summary>
        /// Set of concrete values assigned to this field
        /// </summary>
        public SafeSet<string> FieldValues = new SafeSet<string>();

        /// <summary>
        /// Methods that use this field
        /// </summary>
        public SafeSet<Method> ReadMethods = new SafeSet<Method>();

        /// <summary>
        /// Methods that modify this field from different classes
        /// </summary>
        public SafeDictionary<TypeEx, SafeSet<Method>> WriteMethods = new SafeDictionary<TypeEx, SafeSet<Method>>();

        /// <summary>
        /// Gets textual form of modification type
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static string GetModificationType(FieldStore fs, Method method)
        {
            if (!fs.ModificationTypeDictionary.ContainsKey(method))
                return "UNKNOWN";
            
            FieldModificationType fmt = fs.ModificationTypeDictionary[method];
            return GetModificationTypeStr(fmt);
        }

        /// <summary>
        /// Gets textual form of modification type
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static string GetPreciseModificationType(FieldStore fs, Method method)
        {
            if (!fs.PreciseModificationTypeDictionary.ContainsKey(method))
                return "UNKNOWN";

            FieldModificationType fmt = fs.PreciseModificationTypeDictionary[method];
            return GetModificationTypeStr(fmt);
        }

        private static string GetModificationTypeStr(FieldModificationType fmt)
        {
            switch (fmt)
            {
                case FieldModificationType.DECREMENT: return "DECREMENT";
                case FieldModificationType.INCREMENT: return "INCREMENT";
                case FieldModificationType.INCREMENT_ONE: return "INCREMENT_ONE";
                case FieldModificationType.NULL_SET: return "NULL_SET";
                case FieldModificationType.NON_NULL_SET: return "NON_NULL_SET";
                case FieldModificationType.UNKNOWN: return "UNKNOWN";
                case FieldModificationType.TRUE_SET: return "TRUE_SET";
                case FieldModificationType.FALSE_SET: return "FALSE_SET";
                case FieldModificationType.METHOD_CALL: return "METHOD_CALL";
                default: return "UNKNOWN";
            }
        }

        /// <summary>
        /// Given the string form, retrieves the fieldmodification format
        /// </summary>
        /// <param name="fmt"></param>
        /// <returns></returns>
        public static FieldModificationType GetModificationTypeFromString(string fmt)
        {
            switch (fmt)
            {
                case "DECREMENT": return FieldModificationType.DECREMENT;
                case "INCREMENT": return FieldModificationType.INCREMENT;
                case "INCREMENT_ONE": return FieldModificationType.INCREMENT_ONE;
                case "NULL_SET": return FieldModificationType.NULL_SET;
                case "NON_NULL_SET": return FieldModificationType.NON_NULL_SET;
                case "UNKNOWN": return FieldModificationType.UNKNOWN;
                case "TRUE_SET": return FieldModificationType.TRUE_SET;
                case "FALSE_SET": return FieldModificationType.FALSE_SET;
                case "METHOD_CALL": return FieldModificationType.METHOD_CALL;                    
            }
            return FieldModificationType.UNKNOWN;
        }
    }
}
