using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PexMe.Common
{
    /// <summary>
    /// Includes all constants used in PexMe 
    /// </summary>
    public class PexMeConstants
    {
        public static string PexMeStorageDirectory = System.Environment.GetEnvironmentVariable("SEEKER_REPOSITORY");
        public const string PexMeFactorySuggestionStore = "FactoryStore.bin";
        public const string PexMeDynamicFieldStore = "DynamicFS.bin";
        public const string PexMeDynamicMethodStore = "DynamicMS.bin";
        public const string PexMeDynamicExploredMethods = "ExploredMethods.bin";
        public const string PexMePendingExplorationMethods = "PendingMethods.bin";
        public const string PexMeStaticFieldStore = "StaticFS.bin";
        public const string PexMeStaticMethodStore = "StaticMS.bin";
        public const string ReExecutionStatusFile = "reexecute.txt";

        public const string PexMeDummyTypeName = "dummy";
        public const char PexMePersistenceFormSeparator = '#';

        /// <summary>
        /// A threshold overwhich a method call is considered as a part of loop
        /// rather than using a sequence.
        /// </summary>
        public const int LOOP_THRESHOLD = 2;

        /// <summary>
        /// Flag to turn off the looping concept
        /// </summary>
        public const bool IGNORE_LOOP_FEATURE = false;

        /// <summary>
        /// Maximum number of unsuccessful attempts allowed before giving
        /// up on an uncovered location, under the situation that no progress is made. 
        /// </summary>
        public const int MAX_UNSUCCESSFUL_ATTEMPTS = 1;

        /// <summary>
        /// Maximum number of attempts allowed before giving
        /// up on an uncovered location, even when there is a progress. 
        /// This is mainly to prevent an indefinite running of Pex
        /// </summary>
        public const int TOTAL_MAX_ATTEMPTS = 100;

        /// <summary>
        /// Maximum nesting depth allowed
        /// </summary>
        public const int MAX_ALLOWED_NESTED_DEPTH = 5;

        /// <summary>
        /// Maximum number of attempts for an uncovered location
        /// </summary>
        public const int MAX_UNCOVEREDLOC_ATTEMPTS = 2;

        /// <summary>
        /// Maximum allowed sequences for an uncovered location. Beyond that
        /// the location is declared as failed location
        /// </summary>
        public const int MAX_ALLOWED_SEQUENCES = 100;

        /// <summary>
        /// Flag to turn off the resurrection of uncovered locations
        /// </summary>
        public const bool IGNORE_UNCOVEREDLOC_RESURRECTION = true;

        /// <summary>
        /// Disables an internal functionality where the some private methods
        /// are invoked by some public methods of the same class.
        /// </summary>
        public const bool DISABLE_CALLING_METHODS_WITHIN_CLASS = false;

        /// <summary>
        /// A key used to store all final suggested sequences in the final suggestion store.
        /// </summary>
        public const string DEFAULT_FINAL_SUGGESTION_STORE = "#default#";

        public static HashSet<string> SystemLibraries = new HashSet<string>()
        {
            "mscorlib", "System", "System.Core", "System.Data", "System.Xml"
        };

        /*** CONSTANTS CONTROLLING THE CODE BASE FUNCTIONALITY ***/

        /// <summary>
        /// Flag to turn on and off the advanced constraint solved code from Nikolai. If this
        /// flag is turned off, D4D uses a primitive technique for inferring fitness values
        /// </summary>
        public const bool USE_TERM_SOLVER = false;

        /// <summary>
        /// Ignores the branches that are not covered within the system libraries
        /// </summary>
        public const bool IGNORE_UNCOV_BRANCH_IN_SYSTEM_LIB = false;

        /// <summary>
        /// Enables static analysis to be inter-procedural rather than intra-procedural
        /// </summary>
        public const bool STATIC_INTER_PROCEDURAL_ANALYSIS = true;

        /// <summary>
        /// Helps to debug nested PUTs whose commands are generated internally
        /// </summary>
        public const bool ENABLE_DEBUGGING_MODE = false;

        /// <summary>
        /// Enables or Disables the feature that provide hints for interfaces and abstract classes
        /// </summary>
        public const bool ENABLE_TYPE_HINT_PROVIDER = true;

        /// <summary>
        /// If set to true, Stores complete dynamic database
        /// </summary>
        public const bool ENABLE_DYNAMICDB_STORAGE = false;

        /// <summary>
        /// Maximum allowed size of the static storage. After 100KB, it will be ignored,
        /// since file loading and dumping may take more time compared to re-computing the same
        /// </summary>
        public const int MAX_ALLOWED_STATIC_STORAGE = 100 * 1024;

        /// <summary>
        /// Enables recommender of MSeqGen that scans for the factory methods generated by MSeqGen
        /// </summary>
        public const bool ENABLE_MSEQGEN_RECOMMENDER = false;
    }
}
