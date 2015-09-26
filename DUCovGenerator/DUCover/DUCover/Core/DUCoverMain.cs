using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Metadata;
using NLog.Win32.Targets;
using NLog.Config;
using NLog.Targets;
using NLog;
using DUCover.Static;
using DUCover.Persistence;

namespace DUCover.Core
{
    /// <summary>
    /// A main program for DUCover that sets up all initialization and store all 
    /// static instances used in the tool
    /// </summary>
    public static class DUCoverMain
    {
        static bool bLoggerInitialized = false;

        /// <summary>
        /// Initializing logger
        /// </summary>
        private static Logger logger;

        /// <summary>
        /// Initializes DUCover.
        /// </summary>
        public static void Initialize(AssemblyEx assembly)
        {
            if (!bLoggerInitialized)
            {
                InitializeLogger();
                bLoggerInitialized = true;
                logger = LogManager.GetCurrentClassLogger();
            }

            //Check whether intialization information is required for this assembly
            var shortname = assembly.ShortName;
            if (DUCoverConstants.SystemAssemblies.Contains(shortname))
                return;

            //Analyzes all classes and methods and collects all entities.
            DeclEntityCollector.CollectAllDeclEntitiesInAssembly(assembly);

            //Populate all Def-Use tables
            PopulateAllDUCoverTables();
        }

        /// <summary>
        /// Includes functionality for handling terminating actions
        /// </summary>
        public static void Terminate()
        {
            //Computes ducoverage
            int totalDUPairs, coveredDUPairs, totalDefs, coveredDefs, totalUses, coveredUses;
            ComputeDUCoverage(out totalDUPairs, out coveredDUPairs, out totalDefs, out coveredDefs, out totalUses, out coveredUses);
            logger.Info("Total number of DUPairs: " + totalDUPairs);
            logger.Info("Covered DUPairs: " + coveredDUPairs);
            logger.Info("Def-Use Coverage: " + ((double)coveredDUPairs / (double)totalDUPairs));

            logger.Info("Total number of Defs: " + totalDefs);
            logger.Info("Covered Defs: " + coveredDefs);
            logger.Info("All-Defs Coverage: " + ((double)coveredDefs / (double)totalDefs));

            logger.Info("Total number of Uses: " + totalUses);
            logger.Info("Covered Uses: " + coveredUses);
            logger.Info("All-Uses Coverage: " + ((double)coveredUses / (double)totalUses));

            //logger.Info("Generating PUTs");
            //GeneratePUTsForUncoveredPairs();
            DUCoverStore ade = DUCoverStore.GetInstance();
            MyFileWriter.DumpAllDeclEntity(ade, totalDUPairs, coveredDUPairs, totalDefs, coveredDefs, totalUses, coveredUses);
        }

        /// <summary>
        /// Generates PUTs for all uncovered pairs
        /// </summary>
        private static void GeneratePUTsForUncoveredPairs()
        {
            DUCoverStore dcs = DUCoverStore.GetInstance();
            foreach (var dce in dcs.DeclEntityDic.Values)
            {
                dce.GeneratePUTsForNonCoveredDUPairs();
            }
        }       

        /// <summary>
        /// Initializes logging framework
        /// </summary>
        private static void InitializeLogger()
        {            
            LoggingConfiguration config = new LoggingConfiguration();

            // Create targets and add them to the configuration
            ColoredConsoleTarget consoleTarget = new ColoredConsoleTarget();
            config.AddTarget("console", consoleTarget);            
            FileTarget fileTarget = new FileTarget();
            config.AddTarget("file", fileTarget);

            // Step 3. Set target properties
            consoleTarget.Layout = "${date:format=HH\\:MM\\:ss}: ${message}";

            // set the file
            fileTarget.FileName = DUCoverConstants.LogFileName;
            fileTarget.Layout = "${date:format=HH\\:MM\\:ss}: ${message}";            
            fileTarget.DeleteOldFileOnStartup = true;

            // Step 4. Define rules
            LoggingRule rule1 = new LoggingRule("*", LogLevel.Debug, consoleTarget);
            config.LoggingRules.Add(rule1);
            LoggingRule rule2 = new LoggingRule("*", LogLevel.Debug, fileTarget);
            config.LoggingRules.Add(rule2);        
            
            LogManager.Configuration = config;
        }

        /// <summary>
        /// Populates entire DUCover tables
        /// </summary>
        /// <param name="dcs"></param>
        public static void PopulateAllDUCoverTables()
        {
            DUCoverStore dcs = DUCoverStore.GetInstance();
            foreach (var dce in dcs.DeclEntityDic.Values)
                dce.PopulateDUCoverTable();
        }

        /// <summary>
        /// Computes du coverage
        /// </summary>
        private static void ComputeDUCoverage(out int totalDUPairs, out int coveredDUPairs, out int totalDefs, out int coveredDefs, out int totalUses, out int coveredUses)
        {
            DUCoverStore dcs = DUCoverStore.GetInstance();
            totalDUPairs = coveredDUPairs = 0;
            totalDefs = coveredDefs = 0;
            totalUses = coveredUses = 0;
            foreach (var dce in dcs.DeclEntityDic.Values)
            {
                int tempTotal, tempCovered, temptotalDefs, tempcoveredDefs, temptotalUses, tempcoveredUses;
                dce.ComputeDUCoverage(out tempTotal, out tempCovered, out temptotalDefs, 
                    out tempcoveredDefs, out temptotalUses, out tempcoveredUses);
                totalDUPairs += tempTotal;
                coveredDUPairs += tempCovered;
                totalDefs += temptotalDefs;
                coveredDefs += tempcoveredDefs;
                totalUses += temptotalUses;
                coveredUses += tempcoveredUses;
            }
        }
    }
}
