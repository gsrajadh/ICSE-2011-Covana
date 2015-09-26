using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DUCover.Core
{
    public static class DUCoverConstants
    {
        /// <summary>
        /// Logging all entries
        /// </summary>
        public const string LogFileName = "DUCover.log";

        /// <summary>
        /// Location of DUCover assembly
        /// </summary>
        public const string DUCoverAssemblyVar = "DUCOVER_ASSEMBLY";

        /// <summary>
        /// Environment variable of NUnit.
        /// </summary>
        public const string DUCoverNUnitEnvPath = "DUCOVER_NUNIT_PATH";

        /// <summary>
        /// Represents the location of the DUCover store
        /// </summary>
        public static string DUCoverStoreLocation = System.Environment.GetEnvironmentVariable("DUCOVER_STORE");

        /// <summary>
        /// Represents the filename of the side effect store
        /// </summary>
        public const string SideEffectStoreFile = "SideEffectStore.bin";
        public const string SideEffectStoreDebugFile = "SideEffectStore.txt";

        /// <summary>
        /// All declared entities
        /// </summary>
        public const string DeclEntityFile = "DeclaredEntities.txt";

        /// <summary>
        /// Number of instruction graphs to be stored in cache
        /// </summary>
        public const int MAX_INSTRUCTIONGRAPH_IN_CACHE = 100;

        /// <summary>
        /// System level assemblies that are ignored for monitoring
        /// </summary>
        public static HashSet<string> SystemAssemblies = new HashSet<string>(new string[] { 
            "System",
            "System.Core",
            "System.Data",
            "System.Data.DataSetExtensions",
            "System.Xml",
            "System.Xml.Linq",
            "mscorlib",
            "Microsoft.ExtendedReflection"
            });        
    }
}
