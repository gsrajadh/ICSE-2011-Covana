using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using DUCover.Core;
using System.Runtime.Serialization.Formatters.Binary;
using NLog;
using Microsoft.ExtendedReflection.Metadata;
using System.Runtime.Serialization.Formatters;

namespace DUCover.SideEffectAnalyzer
{
    /// <summary>
    /// Stores all side affects collected for methods
    /// </summary>
    [Serializable]    
    [__DoNotInstrument]
    public class SideEffectStore
    {
        /// <summary>
        /// Initializing logger
        /// </summary>
        private static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Stores the current assembly
        /// </summary>
        public AssemblyEx CurrAssembly
        {
            get;
            set;
        }
        
        [NonSerialized]
        static SideEffectStore ses;

        /// <summary>
        /// Stores all method stores
        /// </summary>        
        System.Collections.Generic.Dictionary<string, SEMethodStore> methodStore = new System.Collections.Generic.Dictionary<string, SEMethodStore>();
        public System.Collections.Generic.Dictionary<string, SEMethodStore> MethodStore
        {
            get { return this.methodStore; }
        }

        private SideEffectStore()
        {
        }

        /// <summary>
        /// Gets an instance of SES
        /// </summary>
        /// <returns></returns>
        public static SideEffectStore GetInstance()
        {
            if (ses == null)
            {
                ses = ReadFromDatabase();                
            }
            return ses;
        }

        /// <summary>
        /// Adds that method defines the field
        /// </summary>
        /// <param name="method"></param>
        /// <param name="field"></param>
        public void AddFieldDefinition(Method method, Field field, int offset)
        {
            //Prevent adding information not related to current assembly
            if (this.CurrAssembly == null || method.Definition.Module.Assembly != this.CurrAssembly)
                return;

            SEMethodStore sem;
            if (!this.methodStore.TryGetValue(method.FullName, out sem))
            {
                sem = new SEMethodStore(method.FullName);
                sem.OptionalMethod = method;
                this.methodStore[method.FullName] = sem;
            }

            sem.AddToDefinedList(field, offset);
        }

        /// <summary>
        /// Returns whether a field is defined or used at a particular offset in a method
        /// </summary>
        /// <param name="method"></param>
        /// <param name="field"></param>
        /// <param name="defined"></param>
        /// <param name="used"></param>
        public bool TryGetFieldDefOrUseByMethod(Method method, Field field, int offset, out bool defined, out bool used)
        {
            defined = false;
            used = false;

            SEMethodStore sem;
            if (this.methodStore.TryGetValue(method.FullName, out sem))
            {
                SEFieldStore sef;
                if (sem.DefinedFieldSet.TryGetValue(field.FullName, out sef))
                {
                    if(sef.AllOffsets.Contains(offset))
                        defined = true;
                }

                if (sem.UsedFieldSet.TryGetValue(field.FullName, out sef))
                {
                    if(sef.AllOffsets.Contains(offset))
                        used = true;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Adds that method modifies the field
        /// </summary>
        /// <param name="method"></param>
        /// <param name="field"></param>
        public void AddFieldUsage(Method method, Field field, int offset)
        {
            //Prevent adding information not related to current assembly
            if (this.CurrAssembly == null || method.Definition.Module.Assembly != this.CurrAssembly)
                return;

            SEMethodStore sem;
            if (!this.methodStore.TryGetValue(method.FullName, out sem))
            {
                sem = new SEMethodStore(method.FullName);
                sem.OptionalMethod = method;
                this.methodStore[method.FullName] = sem;
            }

            sem.AddToUsedList(field, offset);
        }

        /// <summary>
        /// Appends a method store to the one in the repository
        /// </summary>
        /// <param name="sem"></param>
        public void AppendMethodStore(SEMethodStore sem)
        {
            //Prevent adding information not related to current assembly
            if (this.CurrAssembly == null || sem.OptionalMethod.Definition.Module.Assembly != this.CurrAssembly)
                return;

            SEMethodStore repositoryStore;
            if(!this.methodStore.TryGetValue(sem.MethodName, out repositoryStore))
            {
                repositoryStore = new SEMethodStore(sem.MethodName);
                repositoryStore.OptionalMethod = sem.OptionalMethod;
                this.methodStore[sem.MethodName] = repositoryStore;
            }

            //Append fields that are within our assembly
            foreach (var deffield in sem.DefinedFieldSet.Values)
            {
                if (deffield.OptionalField.Definition.Module.Assembly != this.CurrAssembly)
                    continue;
                repositoryStore.DefinedFieldSet[deffield.FullName] = deffield;
            }

            foreach (var usefield in sem.UsedFieldSet.Values)
            {
                if (usefield.OptionalField.Definition.Module.Assembly != this.CurrAssembly)
                    continue;
                repositoryStore.UsedFieldSet[usefield.FullName] = usefield;
            }
        }

        /// <summary>
        /// Reads from the database
        /// </summary>
        /// <returns></returns>
        public static SideEffectStore ReadFromDatabase()
        {
            try
            {
                var filename = Path.Combine(DUCoverConstants.DUCoverStoreLocation, DUCoverConstants.SideEffectStoreDebugFile);
                SideEffectStore ses = new SideEffectStore();
                if (!File.Exists(filename))
                    return ses;
                                
                using (StreamReader sr = new StreamReader(filename))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        line.Trim();
                        string methodname = line;
                        SEMethodStore sem = new SEMethodStore(methodname);
                        ses.MethodStore[methodname] = sem;

                        //Reading defined
                        line = sr.ReadLine().Trim();
                        string[] parts = line.Split(' ');

                        int numDefined = Convert.ToInt32(parts[1]);
                        for (int count = 0; count < numDefined; count++)
                        {
                            line = sr.ReadLine().Trim();
                            string[] lineparts = line.Split(' ');                                                     

                            SEFieldStore sef = new SEFieldStore(lineparts[0]);
                            sem.DefinedFieldSet[lineparts[0]] = sef;
                            for (int tcount = 1; tcount < lineparts.Length; tcount++)
                            {
                                sef.AllOffsets.Add(Convert.ToInt32(lineparts[tcount]));
                            }
                        }

                        //Reading used
                        line = sr.ReadLine().Trim();
                        parts = line.Split(' ');

                        int numUsed = Convert.ToInt32(parts[1]);
                        for (int count = 0; count < numUsed; count++)
                        {
                            line = sr.ReadLine().Trim();
                            string[] lineparts = line.Split(' ');

                            SEFieldStore sef = new SEFieldStore(lineparts[0]);
                            sem.UsedFieldSet[lineparts[0]] = sef;
                            for (int tcount = 1; tcount < lineparts.Length; tcount++)
                            {
                                sef.AllOffsets.Add(Convert.ToInt32(lineparts[tcount]));
                            }
                        }
                    }
                }

                Console.WriteLine("Number of entries read: " + ses.MethodStore.Count);
                return ses;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to deserialize");
                logger.ErrorException("Error occurred while reading from file " + ex.StackTrace, ex);
            }

            return new SideEffectStore();
        }

        /// <summary>
        /// Stores to a persistent plaec
        /// </summary>
        public void DumpToDatabase()
        {
            /*try
            {
                var filename = Path.Combine(DUCoverConstants.DUCoverStoreLocation, DUCoverConstants.SideEffectStoreFile);
                Stream streamWrite = File.Create(filename);
                BinaryFormatter binaryWrite = new BinaryFormatter();
                binaryWrite.AssemblyFormat = FormatterAssemblyStyle.Simple;
                binaryWrite.Serialize(streamWrite, this.methodStore);
                streamWrite.Close();
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error occurred while writing to the file " + ex.StackTrace, ex);
            }*/

            //Dumping information
            var debugfile = Path.Combine(DUCoverConstants.DUCoverStoreLocation, DUCoverConstants.SideEffectStoreDebugFile);
            using (StreamWriter sw = new StreamWriter(debugfile))
            {
                foreach (var semstore in this.methodStore.Values)
                {
                    sw.WriteLine(semstore.MethodName);

                    sw.WriteLine("\tDefined " + semstore.DefinedFieldSet.Count);
                    foreach (var sefstore in semstore.DefinedFieldSet.Values)
                    {
                        sw.Write("\t\t" + sefstore.FullName);
                        foreach (var offset in sefstore.AllOffsets)
                            sw.Write(" " + offset);
                        sw.WriteLine();    
                    }

                    sw.WriteLine("\tUsed " + semstore.UsedFieldSet.Count);
                    foreach (var sefstore in semstore.UsedFieldSet.Values)
                    {
                        sw.Write("\t\t" + sefstore.FullName);
                        foreach (var offset in sefstore.AllOffsets)
                            sw.Write(" " + offset);
                        sw.WriteLine();
                    }
                }
            }

            Console.WriteLine("Number of entries dumped to database: " + ses.MethodStore.Count);
        }
    }
}
