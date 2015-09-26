using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Metadata;
using DUCover.Component;
using Microsoft.ExtendedReflection.ComponentModel;
using PexMe.Core;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.ExtendedReflection.Symbols;
using DUCover.Graph;

namespace DUCover.Core
{
    /// <summary>
    /// Stores all declared entities in a program
    /// </summary>
    public class DUCoverStore
    {
        SafeDictionary<string, DeclClassEntity> declEntityDic = new SafeDictionary<string, DeclClassEntity>();
        DUCoverEngine dce;
        public IPexComponent Host
        {
            get;
            set;
        }

        public DUCoverStore()
        {
            this.dce = new DUCoverEngine();
            this.Host = this.dce.GetService<PexMeDynamicDatabase>();
        }

        public SafeDictionary<string, DeclClassEntity> DeclEntityDic
        {
            get 
            {
                return this.declEntityDic;
            }
        }

        /// <summary>
        /// Maintains a cache of instruction graphs to avoid reloading unnecessarily
        /// </summary>
        private Dictionary<int, InstructionGraph> igCache = new Dictionary<int, InstructionGraph>();
        public InstructionGraph GetInstructionGraph(Method method)
        {
            //to prevent storing all graphs and avoid memory issues
            if (this.igCache.Count > DUCoverConstants.MAX_INSTRUCTIONGRAPH_IN_CACHE)
                this.igCache.Clear();
            
            InstructionGraph ig;
            if (this.igCache.TryGetValue(method.GlobalIndex, out ig))
                return ig;

            ig = new InstructionGraph(method);
            this.igCache[method.GlobalIndex] = ig;
            return ig;
        }

        /// <summary>
        /// Gets a line number of a method
        /// </summary>
        /// <returns></returns>
        public int GetLineNumberOfOffset(Method method, int offset)
        {
            int line = -1;            
            var methodDef = method.Definition;
            ISymbolManager smanager = this.Host.GetService<ISymbolManager>();
            SequencePoint sp;
            if (smanager.TryGetSequencePoint(methodDef, offset, out sp))
            {
                line = sp.Line;                
            }
            return line;
        }

        static DUCoverStore ade = null;
        public static DUCoverStore GetInstance()
        {
            if (ade == null)
            {
                ade = new DUCoverStore();                
            }
            return ade;
        }

        /// <summary>
        /// Adds a declared entity. Returns either the existing one or creates a new one
        /// </summary>
        /// <param name="de"></param>
        public void AddToDeclEntityDic(TypeDefinition td, out DeclClassEntity dce)
        {            
            if (!this.declEntityDic.TryGetValue(td.FullName, out dce))
            {
                TypeEx type = td.Instantiate(MethodOrFieldAnalyzer.GetGenericTypeParameters(this.Host, td));
                dce = new DeclClassEntity(type);
                declEntityDic[type.FullName] = dce;
            }
        }
    }
}
