using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Collections;
using PexMe.Core;
using Microsoft.Pex.Engine.ComponentModel;

namespace DUCover.Core
{
    /// <summary>
    /// Represents a declared class
    /// </summary>
    public class DeclClassEntity
        : IDeclEntity
    {
        /// <summary>
        /// represents the inbuilt type
        /// </summary>
        TypeEx td;

        IPexComponent host;

        /// <summary>
        /// stores all field entities
        /// </summary>
        SafeDictionary<string, DeclFieldEntity> fieldEntities = new SafeDictionary<string,DeclFieldEntity>();
        public SafeDictionary<string, DeclFieldEntity> FieldEntities
        {
            get
            {
                return this.fieldEntities;
            }
        }

        public DeclClassEntity(TypeEx td)
        {
            this.td = td;
            this.host = DUCoverStore.GetInstance().Host;
        }

        /// <summary>
        /// Total number of DU pairs
        /// </summary>
        public int TotalDUPairs
        {
            get;
            set;
        }

        /// <summary>
        /// Number of covered DU pairs
        /// </summary>
        public int CoveredDUPairs
        {
            get;
            set;
        }

        /// <summary>
        /// Total number of defs
        /// </summary>
        public int TotalDefs
        {
            get;
            set;
        }

        public int CoveredDefs
        {
            get;
            set;
        }

        public int TotalUses
        {
            get;
            set;
        }

        public int CoveredUses
        {
            get;
            set;
        }

        /// <summary>
        /// Adds a field entity
        /// </summary>
        /// <param name="fd"></param>
        public void AddFieldEntity(TypeDefinition td, FieldDefinition fd)
        {         
            if (!fieldEntities.ContainsKey(fd.FullName))
            {
                var field = fd.Instantiate(MethodOrFieldAnalyzer.GetGenericTypeParameters(this.host, td));
                DeclFieldEntity dfe = new DeclFieldEntity(field);
                fieldEntities[field.FullName] = dfe;
            }
        }

        public bool TryGetFieldEntity(TypeDefinition td, FieldDefinition fd, out DeclFieldEntity dfe)
        {            
            var field = fd.Instantiate(MethodOrFieldAnalyzer.GetGenericTypeParameters(this.host, td));
            return fieldEntities.TryGetValue(fd.FullName, out dfe);
        }

        public override int GetHashCode()
        {
            return this.td.FullName.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var otherObj = obj as DeclClassEntity;
            if (otherObj == null)
                return false;

            return this.td.Equals(otherObj.td);
        }

        public override string ToString()
        {
            return this.td.FullName;
        }

        #region IDeclEntity Members
        public void PopulateDUCoverTable()
        {
            foreach (var dfe in fieldEntities.Values)
            {
                dfe.PopulateDUCoverTable();
            }
        }

        /// <summary>
        /// Computes the DUCoverage. Also computes all-defs and all-uses coverage
        /// </summary>
        /// <param name="totalDUPairs"></param>
        /// <param name="coveredDUPairs"></param>
        public void ComputeDUCoverage(out int totalDUPairs, out int coveredDUPairs, out int totalDefs, out int coveredDefs, out int totalUses, out int coveredUses)
        {
            totalDUPairs = coveredDUPairs = 0;
            totalDefs = coveredDefs = 0;
            totalUses = coveredUses = 0;
            foreach (var dfe in fieldEntities.Values)
            {
                int tempTotal, tempCovered, temptotalDefs, tempcoveredDefs, temptotalUses, tempcoveredUses;
                dfe.ComputeDUCoverage(out tempTotal, out tempCovered, out temptotalDefs,
                    out tempcoveredDefs, out temptotalUses, out tempcoveredUses);
                totalDUPairs += tempTotal;
                coveredDUPairs += tempCovered;
                totalDefs += temptotalDefs;
                coveredDefs += tempcoveredDefs;
                totalUses += temptotalUses;
                coveredUses += tempcoveredUses;
            }

            this.TotalDUPairs = totalDUPairs;
            this.CoveredDUPairs = coveredDUPairs;
            this.TotalDefs = totalDefs;
            this.CoveredDefs = coveredDefs;
            this.TotalUses = totalUses;
            this.CoveredUses = coveredUses;
        }

        /// <summary>
        /// Generates a PUT for uncovered entry
        /// </summary>
        public void GeneratePUTsForNonCoveredDUPairs()
        {
            foreach (var dfe in fieldEntities.Values)
            {
                dfe.GeneratePUTsForNonCoveredDUPairs();
            }
        }
        #endregion
    }
}
