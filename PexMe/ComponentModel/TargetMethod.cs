using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ExtendedReflection.Metadata;

namespace PexMe.Core
{
    enum TARGET_METHOD_TYPE {STATIC, DYNAMIC};

    /// <summary>
    /// Stores the target method that is desirable for achieving a branching condition
    /// Simply a wrapper for storing the additional information for the method
    /// </summary>
    internal class TargetMethodWrapper
    {
        /// <summary>
        /// Actual target method
        /// </summary>
        public Method TargetMethod = null;

        /// <summary>
        /// Type of analysis used for inferring this method
        /// </summary>
        public TARGET_METHOD_TYPE Analysis_Type = TARGET_METHOD_TYPE.DYNAMIC;

        public override string ToString()
        {
            return this.TargetMethod != null? this.TargetMethod.ToString() : "";
        }

        /// <summary>
        /// Gets the string form of target method type
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static string Get_Method_Type(TARGET_METHOD_TYPE tmt)
        {
            switch (tmt)
            {
                case TARGET_METHOD_TYPE.DYNAMIC: return "Dynamic";
                case TARGET_METHOD_TYPE.STATIC: return "Static";
                default: return "Unknown";
            }
        }
    }
}
