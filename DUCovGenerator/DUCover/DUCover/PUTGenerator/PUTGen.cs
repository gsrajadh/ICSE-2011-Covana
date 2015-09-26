using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Collections;
using DUCover.Core;
using NLog;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.Pex.Engine.Explorable;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.ExtendedReflection.Feedback;

namespace DUCover.PUTGenerator
{
    /// <summary>
    /// Generates additional PUTs based on uncovered DU Pairs for new cycle.
    /// </summary>
    public class PUTGen
    {
        /// <summary>
        /// Initializing logger
        /// </summary>
        private static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Stores all generated PUTs.
        /// </summary>
        SafeDictionary<string, SafeSet<string>> PUTDictionary = new SafeDictionary<string, SafeSet<string>>();

        DUCoverStore dcs;
        IPexComponent host;
        public PUTGen()
        {
            this.dcs = DUCoverStore.GetInstance();
            this.host = this.dcs.Host;
        }

        public static PUTGen putgen = null;
        public static PUTGen GetInstance()
        {
            if (putgen == null)
                putgen = new PUTGen();
            return putgen;
        }

        /// <summary>
        /// Generates a put for an uncovered DUCoverStoreEntry
        /// </summary>
        /// <param name="dcse"></param>
        public void GeneratePUT(DUCoverStoreEntry dcse)
        { 
            //Check whether the TypeEx of field, and both the methods is same. If not raise warning
            TypeEx fieldTypeEx;
            if (!dcse.Field.TryGetDeclaringType(out fieldTypeEx))
            {
                logger.Warn("Failed to get the declaring type of the field " + dcse.Field.FullName);
                return;
            }

            TypeEx defMethodEx;
            if (!dcse.DefMethod.TryGetDeclaringType(out defMethodEx))
            {
                logger.Warn("Failed to get the declaring type of the method " + dcse.DefMethod.FullName);
                return;
            }

            TypeEx useMethodEx;
            if (!dcse.UseMethod.TryGetDeclaringType(out useMethodEx))
            {
                logger.Warn("Failed to get the declaring type of the method " + dcse.UseMethod.FullName);
                return;
            }

            if (!fieldTypeEx.Equals(defMethodEx) || !defMethodEx.Equals(useMethodEx))
            {
                logger.Warn("All declaring types of field, def-method, and use-method should be same for generating PUT. Condition failed for " + dcse.ToString());
                return;
            }

            try
            {
                string putgenerated = "";

                SafeSet<string> existingPUTs;
                if (!this.PUTDictionary.TryGetValue(fieldTypeEx.FullName, out existingPUTs))
                {
                    existingPUTs = new SafeSet<string>();
                    this.PUTDictionary[fieldTypeEx.FullName] = existingPUTs;
                }

                existingPUTs.Add(putgenerated);
            }
            catch (Exception ex)
            {
                logger.ErrorException("Failed to generate PUT for " + dcse.ToString() + " " + ex.Message, ex);
            }
        }


        public void GeneratePUTFiles()
        { 
        }
    }
}
