using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Collections;
using NLog;
using DUCover.Graph;
using DUCover.PUTGenerator;

namespace DUCover.Core
{
    public enum DEFUSE_FEASIBILITY_TYPE {DEF_FEASIBLE, DEF_INFEASIBLE, USE_FEASIBLE, USE_INFEASIBLE};

    /// <summary>
    /// Stores a definition of the field. Used for storing the field use also
    /// </summary>
    public class FieldDefUseStore
    {
        internal Method @Method;
        internal int Offset;
        internal Field Field;

        /// <summary>
        /// An entry is added to both def or use list, if the associated method call's side-effects are not known
        /// by the static analysis
        /// </summary>
        internal Method UnknownSideEffectMethod;

        public FieldDefUseStore(Field field, Method method, int offset)
        {
            this.Field = field;
            this.Method = method;
            this.Offset = offset;
        }

        public override bool Equals(object obj)
        {
            var lfds = obj as FieldDefUseStore;
            if (lfds == null)
                return false;

            if (this.Offset != lfds.Offset)
                return false;

            if (!this.Field.Equals(lfds.Field))
                return false;

            if (!this.Method.Equals(lfds.Method))
                return false;

            return true;
        }

        public override int  GetHashCode()
        {
            return this.Field.FullName.GetHashCode();
        }

        public override string ToString()
        {
            var sb = new StringBuilder(this.Method.FullName + this.Offset);
            if (this.UnknownSideEffectMethod != null)
                sb.Append("(Reason: " + this.UnknownSideEffectMethod.FullName + ")");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Represents a field entity 
    /// </summary>
    public class DeclFieldEntity
        : IDeclEntity
    {
        /// <summary>
        /// Initializing logger
        /// </summary>
        private static Logger logger = LogManager.GetCurrentClassLogger();
        
        /// <summary>
        /// Represents the field defintion
        /// </summary>
        Field fd;

        /// <summary>
        /// List of definitions for this field. Each key should be of the following form:
        /// "FullNameofTheMethod + offset of the instruction within the method"
        /// This instruction is either a method call that updates field or stfld instruction
        /// </summary>
        SafeDictionary<FieldDefUseStore, int> defDic = new SafeDictionary<FieldDefUseStore, int>();
        public SafeDictionary<FieldDefUseStore, int> DefDic
        {
            get
            {
                return this.defDic;
            }
        }

        /// <summary>
        /// List of usages for the field. The syntax is the same as defList.
        /// </summary>
        SafeDictionary<FieldDefUseStore, int> useDic = new SafeDictionary<FieldDefUseStore, int>();
        public SafeDictionary<FieldDefUseStore, int> UseDic
        {
            get
            {
                return this.useDic;
            }
        }

        /// <summary>
        /// caches all feasible definitions to avoid recomputation. Cannot be used
        /// for Def-Use pairs within the same method
        /// </summary>
        SafeDictionary<FieldDefUseStore, DEFUSE_FEASIBILITY_TYPE> feasibilityDicCache 
            = new SafeDictionary<FieldDefUseStore, DEFUSE_FEASIBILITY_TYPE>();
        
        /// <summary>
        /// A list that stores unknown stuff at this point, which could be either def or use.
        /// This will be decided later at the further stages
        /// </summary>
        SafeSet<FieldDefUseStore> defOrUseSet = new SafeSet<FieldDefUseStore>();
        public SafeSet<FieldDefUseStore> DefOrUseSet
        {
            get
            {
                return this.defOrUseSet;
            }
        }
        

        /// <summary>
        /// Stores the DUCoverage information
        /// </summary>
        SafeDictionary<DUCoverStoreEntry, int> duCoverageTable = new SafeDictionary<DUCoverStoreEntry, int>();
        public SafeDictionary<DUCoverStoreEntry, int> DUCoverageTable
        {
            get
            {
                return this.duCoverageTable;
            }
        }

        /// <summary>
        /// Total number of DU pairs
        /// </summary>
        public int TotalDUPairs
        {
            get; set;
        }

        /// <summary>
        /// Number of covered DU pairs
        /// </summary>
        public int CoveredDUPairs
        {
            get; set;
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

        public DeclFieldEntity(Field fd)
        {
            this.fd = fd;
        }        

        public void AddToDefList(Method md, int defoffset)
        {
            FieldDefUseStore fdus = new FieldDefUseStore(this.fd, md, defoffset);
            this.defDic[fdus] = 0;
        }

        public void AddToUseList(Method md, int useoffset)
        {
            FieldDefUseStore fdus = new FieldDefUseStore(this.fd, md, useoffset);
            this.useDic[fdus] = 0;
        }

        public void AddToDefOrUseList(Method md, int useoffset, Method unknownSideEffectMethod)
        {
            FieldDefUseStore fdus = new FieldDefUseStore(this.fd, md, useoffset);
            fdus.UnknownSideEffectMethod = unknownSideEffectMethod;
            this.defOrUseSet.Add(fdus);
        }

        /// <summary>
        /// Updates the table
        /// </summary>
        /// <param name="defm"></param>
        /// <param name="defcl"></param>
        /// <param name="usem"></param>
        /// <param name="usecl"></param>
        public bool UpdateDUCoverageTable(Method defmd, int defoffset, Method usemd, int useoffset)
        {
            var dcse = new DUCoverStoreEntry(this.fd, defmd, defoffset, usemd, useoffset);            
            int existingVal = 0;
            if (!this.duCoverageTable.TryGetValue(dcse, out existingVal))
            {
                //Found a new entry in dynamic analysis, not found in static
                //logger.Warn("Dynamic Analysis new entry for field: " + this.fd.FullName + " " + dcse.ToString());
                return false;
            }
            else
            {
                existingVal++;
                this.duCoverageTable[dcse] = existingVal;
            }

            return true;
        }

        public override string ToString()
        {
            return this.fd.FullName;
        }

        #region IDeclEntity Members
        /// <summary>
        /// Populates all pair-wise combinations of defs and uses identified through static analysis
        /// </summary>
        public void PopulateDUCoverTable()
        {
            DUCoverStore dcs = DUCoverStore.GetInstance();

            SafeSet<FieldDefUseStore> allDefs = new SafeSet<FieldDefUseStore>();
            allDefs.AddRange(this.DefDic.Keys);
            allDefs.AddRange(this.DefOrUseSet);

            SafeSet<FieldDefUseStore> allUses = new SafeSet<FieldDefUseStore>();
            allUses.AddRange(this.UseDic.Keys);
            allUses.AddRange(this.DefOrUseSet);
            int numInfeasible = 0;

            //Compute pair-wise combinations
            foreach (var defEntry in allDefs)
            {
                foreach (var useEntry in allUses)
                {
                    //Ignore the trivial entries that involve just a combination of setter and getter methods
                    if (defEntry.Method.ShortName.StartsWith("set_") && useEntry.Method.ShortName.StartsWith("get_"))
                        continue;
                    if (!this.IsFeasibleDUCoverEntry(dcs, defEntry, useEntry))
                    {
                        numInfeasible++;
                        continue;
                    }
                    
                    DUCoverStoreEntry dcse = new DUCoverStoreEntry(this.fd, defEntry.Method, defEntry.Offset,
                        useEntry.Method, useEntry.Offset);
                    
                    if (defEntry.UnknownSideEffectMethod != null)
                    {
                        dcse.Def_UnknownSideEffectMethod = defEntry.UnknownSideEffectMethod;
                        dcse.DefUnsure = true;
                    }

                    if (useEntry.UnknownSideEffectMethod != null)
                    {
                        dcse.Use_UnknownSideEffectMethod = useEntry.UnknownSideEffectMethod;
                        dcse.UseUnsure = true;
                    }

                    if(!this.duCoverageTable.ContainsKey(dcse))
                        this.duCoverageTable[dcse] = 0;
                }
            }

            //Clear all the cached entries.
            this.feasibilityDicCache.Clear(); 

            logger.Debug("Detected infeasible du-pairs for field " + this.fd.FullName + "(" + numInfeasible + ")");
        }

        /// <summary>
        /// Checks whether a definition and use are in different methods
        /// </summary>
        /// <param name="dcse"></param>
        /// <returns></returns>
        private bool IsFeasibleDUCoverEntry(DUCoverStore dcs, FieldDefUseStore fdef, FieldDefUseStore fuse)
        {
            //if (fdef.Method.FullName.Contains("AddVertex") && fuse.Method.FullName.Contains("AddVertex") && fdef.Field.FullName.Contains("m_VertexOutEdges")
            //    && fdef.Offset == 13 && fuse.Offset == 2)
            //{
            //    int i = 0;
            //}

            //check whether the def and use are in the same method
            if (fdef.Method.Equals(fuse.Method))
            {
                var otherFDefOffsets = this.GetOtherDefOffsetsInMethod(fdef.Method, fdef);                
                InstructionGraph ig = dcs.GetInstructionGraph(fdef.Method);
                DepthFirstTraversal dft = new DepthFirstTraversal(ig);
                var source = ig.GetVertex(fdef.Offset);
                var target = ig.GetVertex(fuse.Offset);                
                return dft.HasDefClearPathBetweenNodes(source, target, otherFDefOffsets);
            }
            else
            {
                var otherFDefOffsets = this.GetOtherDefOffsetsInMethod(fdef.Method, fdef);
                if (otherFDefOffsets.Count > 0)
                {
                    if (this.HasRedefinition(dcs, fdef, otherFDefOffsets))
                        return false;                    
                }

                otherFDefOffsets = this.GetOtherDefOffsetsInMethod(fuse.Method, fdef);
                if (otherFDefOffsets.Count > 0)
                {
                    if (this.HasDefinitionFromRootToUseNode(dcs, fuse, otherFDefOffsets))
                        return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Removes if there are any infeasible definitions
        /// </summary>
        private void RemoveInfeasibleDefs(DUCoverStore dcs)
        {
            //There are either zero or one definitions
            if (this.defDic.Count <= 1)
                return;

            SafeSet<FieldDefUseStore> infeasibleDefs = new SafeSet<FieldDefUseStore>();
            foreach (var fdef in this.defDic.Keys)
            {
                var otherFDefOffsets = this.GetOtherDefOffsetsInMethod(fdef.Method, fdef);
                if (otherFDefOffsets.Count == 0)
                    continue;

                if (HasRedefinition(dcs, fdef, otherFDefOffsets))
                    infeasibleDefs.Add(fdef);
            }
            this.defDic.RemoveRange(infeasibleDefs);
        }

        /// <summary>
        /// Gets all other offsets within the method, where the field is defined
        /// </summary>
        /// <param name="method"></param>
        /// <param name="safeSet"></param>
        /// <returns></returns>
        private HashSet<int> GetOtherDefOffsetsInMethod(Method method, FieldDefUseStore currfdef)
        {
            HashSet<int> otherOffsets = new HashSet<int>();
            foreach (var fdef in this.defDic.Keys)
            {
                if (fdef.Equals(currfdef))
                    continue;

                if (fdef.Method.Equals(method))
                    otherOffsets.Add(fdef.Offset);
            }

            return otherOffsets;
        }

        /// <summary>
        /// Checks whether the fdef has a redefinition from the current not to the end of the method
        /// </summary>
        /// <param name="fdef"></param>
        /// <param name="otherDefinedOffsets"></param>
        /// <returns></returns>
        private bool HasRedefinition(DUCoverStore dcs, FieldDefUseStore fdef, HashSet<int> otherDefinedOffsets)
        {
            DEFUSE_FEASIBILITY_TYPE feasibilityVal;
            if (this.feasibilityDicCache.TryGetValue(fdef, out feasibilityVal))
            {
                if (feasibilityVal == DEFUSE_FEASIBILITY_TYPE.DEF_FEASIBLE)
                    return false;
                else
                    return true;
            }

            InstructionGraph ig = dcs.GetInstructionGraph(fdef.Method);
            DepthFirstTraversal dft = new DepthFirstTraversal(ig);
            InstructionVertex iv = ig.GetVertex(fdef.Offset);
            var result = dft.HasDefClearPathToEnd(iv, otherDefinedOffsets);

            if (result)
                this.feasibilityDicCache[fdef] = DEFUSE_FEASIBILITY_TYPE.DEF_FEASIBLE;
            else
                this.feasibilityDicCache[fdef] = DEFUSE_FEASIBILITY_TYPE.DEF_INFEASIBLE;
            
            return !result;
        }

        /// <summary>
        /// Checks whether there is a definition from the beginning to the use node
        /// </summary>
        /// <param name="fdef"></param>
        /// <param name="otherDefinedOffsets"></param>
        /// <returns></returns>
        private bool HasDefinitionFromRootToUseNode(DUCoverStore dcs, FieldDefUseStore fuse, HashSet<int> otherDefinedOffsets)
        {
            DEFUSE_FEASIBILITY_TYPE feasibilityVal;
            if (this.feasibilityDicCache.TryGetValue(fuse, out feasibilityVal))
            {
                if (feasibilityVal == DEFUSE_FEASIBILITY_TYPE.USE_FEASIBLE)
                    return false;
                else
                    return true;
            }

            InstructionGraph ig = dcs.GetInstructionGraph(fuse.Method);
            DepthFirstTraversal dft = new DepthFirstTraversal(ig);
            InstructionVertex iv = ig.GetVertex(fuse.Offset);
            var result = dft.HasDefClearPathFromBeginning(iv, otherDefinedOffsets);

            if (result)
                this.feasibilityDicCache[fuse] = DEFUSE_FEASIBILITY_TYPE.USE_FEASIBLE;
            else
                this.feasibilityDicCache[fuse] = DEFUSE_FEASIBILITY_TYPE.USE_INFEASIBLE;

            return !result;
        }

        public void ComputeDUCoverage(out int totalDUPairs, out int coveredDUPairs, out int totalDefs, out int coveredDefs, out int totalUses, out int coveredUses)
        {
            totalDUPairs = this.duCoverageTable.Count;
            coveredDUPairs = coveredDefs = coveredUses = 0;
            totalDefs = this.defDic.Count;
            totalUses = this.useDic.Count;

            //Computing def-uses
            foreach (var dcse in this.duCoverageTable.Keys)
            {
                var value = this.duCoverageTable[dcse];
                if (value > 0)
                {
                    coveredDUPairs++;                   
                    var defpart = new FieldDefUseStore(dcse.Field, dcse.DefMethod, dcse.DefOffset);
                    var existingDefValue = 0;
                    if (this.defDic.TryGetValue(defpart, out existingDefValue))
                        this.defDic[defpart] = existingDefValue + 1;
                                        
                    var usepart = new FieldDefUseStore(dcse.Field, dcse.UseMethod, dcse.UseOffset);
                    var existingUseValue = 0;
                    if (this.useDic.TryGetValue(usepart, out existingUseValue))
                        this.useDic[usepart] = existingUseValue + 1;
                }
            }

            //Computing defs. A def is considered as covered if it is exercised by one use
            foreach (var defvalue in this.defDic.Values)
            {
                if (defvalue > 0)
                    coveredDefs++;
            }

            //Computing uses. A use is considered as covered if all its pairs with defs are covered
            foreach (var usevalue in this.useDic.Values)
            {
                if (usevalue >= totalDefs)
                    coveredUses++;
            }

            this.TotalDUPairs = totalDUPairs;
            this.CoveredDUPairs = coveredDUPairs;
            this.TotalDefs = totalDefs;
            this.CoveredDefs = coveredDefs;
            this.TotalUses = totalUses;
            this.CoveredUses = coveredUses;
        }        

        public void GeneratePUTsForNonCoveredDUPairs()
        {
            PUTGen pgen = PUTGen.GetInstance();
            foreach (var dcse in this.duCoverageTable.Keys)
            {
                var value = this.duCoverageTable[dcse];
                if (value == 0)
                {
                    pgen.GeneratePUT(dcse);
                }
            }
        }
        #endregion
    }
}
