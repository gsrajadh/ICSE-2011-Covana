using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DUCover.Core;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using NLog;

namespace DUCover.Persistence
{
    /// <summary>
    /// Handles outputting the content to files
    /// </summary>
    [__DoNotInstrument]
    public static class MyFileWriter
    {
        private static Logger logger = LogManager.GetLogger("MyFileWriter");

        public static void DumpAllDeclEntity(DUCoverStore ade, int totalDUPairs, int coveredDUPairs,
            int totalDefs, int coveredDefs, int totalUses, int coveredUses)
        {
            //Dump the dynamic field store that includes information of which method modify which fields
            try
            {
                var filename = DUCoverConstants.DeclEntityFile;
                using (StreamWriter sw = new StreamWriter(filename))
                {
                    sw.WriteLine("Total number of DUPairs: " + totalDUPairs);
                    sw.WriteLine("\tCovered DUPairs: " + coveredDUPairs);
                    sw.WriteLine("\tDef-Use Coverage: " + ((double)coveredDUPairs / (double)totalDUPairs));

                    sw.WriteLine("Total number of Defs: " + totalDefs);
                    sw.WriteLine("\tCovered Defs: " + coveredDefs);
                    sw.WriteLine("\tAll-Defs Coverage: " + ((double)coveredDefs / (double)totalDefs));

                    sw.WriteLine("Total number of Uses: " + totalUses);
                    sw.WriteLine("\tCovered Uses: " + coveredUses);
                    sw.WriteLine("\tAll-Uses Coverage: " + ((double)coveredUses / (double)totalUses));

                    foreach (var de in ade.DeclEntityDic.Values)
                    {
                        sw.WriteLine("ClassName: " + de.ToString());
                        sw.WriteLine("Total DU pairs: " + de.TotalDUPairs);
                        sw.WriteLine("Covered DU pairs: " + de.CoveredDUPairs);
                        sw.WriteLine("Total defs: " + de.TotalDefs);
                        sw.WriteLine("Covered defs: " + de.CoveredDefs);
                        sw.WriteLine("Total uses: " + de.TotalUses);
                        sw.WriteLine("Covered uses: " + de.CoveredUses);
                        sw.WriteLine();

                        foreach(var dfe in de.FieldEntities.Values)
                        {
                            sw.WriteLine("\tField: " + dfe.ToString());
                            sw.WriteLine("\tTotal DU pairs: " + dfe.TotalDUPairs);
                            sw.WriteLine("\tCovered DU pairs: " + dfe.CoveredDUPairs);
                            sw.WriteLine("\tTotal defs: " + dfe.TotalDefs);
                            sw.WriteLine("\tCovered defs: " + dfe.CoveredDefs);
                            sw.WriteLine("\tTotal uses: " + dfe.TotalUses);
                            sw.WriteLine("\tCovered uses: " + dfe.CoveredUses);
                            sw.WriteLine("\tAll Def Entries");
                            foreach (var defkey in dfe.DefDic.Keys)
                            {
                                sw.WriteLine("\t\t" + defkey.ToString() + " " + dfe.DefDic[defkey]);
                            }
                            
                            sw.WriteLine("\t All Use Entries");
                            foreach (var usekey in dfe.UseDic.Keys)
                            {
                                sw.WriteLine("\t\t" + usekey.ToString() + " " + dfe.UseDic[usekey]);
                            }

                            sw.WriteLine("\t All DefOrUse (currently unknown) entries");
                            foreach (var deforusekey in dfe.DefOrUseSet)
                            {
                                sw.WriteLine("\t\t" + deforusekey.ToString());
                            }

                            sw.WriteLine("\t All Def-Use Entries");
                            var dudic = dfe.DUCoverageTable;
                            foreach (var defusekey in dudic.Keys)
                            {
                                sw.WriteLine("\t\t" + defusekey + " : " + dudic[defusekey]);
                            }
                        }

                        sw.WriteLine("====================================================");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("Failed to write contents of all declared entities", ex);
            }
        }
    }
}
