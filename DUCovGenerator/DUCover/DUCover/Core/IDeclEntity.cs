using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DUCover.Core
{
    /// <summary>
    /// represents an interface for all declared entities
    /// </summary>
    public interface IDeclEntity
    {
        /// <summary>
        /// Fills DUCover table based on static information collected.
        /// </summary>
        void PopulateDUCoverTable();

        /// <summary>
        /// Computes the DUCoverage
        /// </summary>
        /// <param name="totalDUPairs"></param>
        /// <param name="coveredDUPairs"></param>
        void ComputeDUCoverage(out int totalDUPairs, out int coveredDUPairs, out int totalDefs, out int coveredDefs, out int totalUses, out int coveredUses);

        /// <summary>
        /// Generates a PUT for uncovered entry
        /// </summary>
        void GeneratePUTsForNonCoveredDUPairs();
    }
}
