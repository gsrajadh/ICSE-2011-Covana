using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Windows.Forms;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.Pex.Engine.Logging;
using Covana;
using Covana.Analyzer;
using Covana.ResultTrackingExtrator;

namespace CoverageAnalysisForm
{
    public partial class CovanaForm : Form
    {
        private string infoDirectory;
        private string assemblyName;
        private string objectCreationProblemFileName;
        private string notCoveredBranchFileName;
        private string resultTrackingInfoFileName;
        private StringBuilder Log = new StringBuilder("CovLog: \n");
        private InsufficientObjectFactoryFieldInfo insufficientObjectFactoryFieldInfo;
        private string insufficientObjectFactoryFieldInfoFileName;
        private ProblemInfo _problemInfo;
        private NonCoveredBranchInfo _nonCoveredBranchInfo;
        private InsufficientFieldInfoWithBranchInfo insufficientFieldInfoWithBranchInfo;
        private string insufficientFieldInfoWithBranchInfoFileName;
        private Dictionary<Problem, HashSet<BranchInfo>> ProblemWithBranches = new Dictionary<Problem, HashSet<BranchInfo>>();
        private ResultTrackingInfo resultTrackingInfo;
        private HashSet<CandidateObjectCreationProblem> _candidateObjectCreationProblems;
        private string candidateObjectCreationProblemFileName;
        private List<string> unInstrumentedMethodNames;
        private string uninstrumentedMethodProblemFileName;

        private Dictionary<string, HashSet<BranchInfo>> uninstrumentedMethodsFoundInObj =
            new Dictionary<string, HashSet<BranchInfo>>();

        private string foundUninstrumentedInObjectCreationFileName;
        private string exceptionExternalMethodsFileName;
        public HashSet<ExceptionExternalMethod> ExceptionExternalMethods = new HashSet<ExceptionExternalMethod>();
        public HashSet<string> FoundTypes = new HashSet<string>();
        private string foundObjectTypeFileName;


        public CovanaForm()
        {
            InitializeComponent();
        }

        public CovanaForm(string[] strings)
        {
            InitializeComponent();
           
            if(strings.Length > 0){
                assemblyName = strings[0];
                infoDirectory = strings[0];
                txtAssemblyName.Text = assemblyName;
            }
            
        }

        private Object LoadInfoFromFile(Object reference, string fileName)
        {
            if (File.Exists(fileName)) //if exisitng field access file exists, read it in
            {
                try
                {
                    Stream streamRead = File.OpenRead(fileName);
                    BinaryFormatter binaryRead = new BinaryFormatter();
                    reference = binaryRead.Deserialize(streamRead);
                    streamRead.Close();
                    Log.AppendLine("Loading info from " + fileName + "successfully!");
                    return reference;
                }
                catch (Exception e)
                {
                    Log.AppendLine("reading info from " + fileName + e);
                }
            }
            return null;
        }

        private void CovanaForm_Load(object sender, EventArgs e)
        {
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                LoadInfosFromFiles();

                UpdateBranchTree();

                UpdateProblemTree();

                UpdateUninstrumentedMethodList();

                UpdateUninstrumentedMethodInBranchList();

                UpdateCandidateObjectCreationProblemList();

                UpdateFoundObjectCreationProblemList();
            }
            catch (Exception exception)
            {
                Log.AppendLine(exception.ToString());
                ShowError(exception.ToString());
            }
            finally
            {
                DumpInfoToDebugFile(Log.ToString(), infoDirectory + assemblyName + ".covLog.txt");
            }
        }

        private void UpdateFoundObjectCreationProblemList()
        {
            foundObjectTypeList.Items.Clear();
            foreach (string foundType in FoundTypes)
            {
                foundObjectTypeList.Items.Add(foundType);
            }
            foundObjectTypeCountlabel.Text = FoundTypes.Count.ToString();
        }

        private void UpdateBranchTree()
        {
            nonCoveredBranchTreeView.Nodes.Clear();
            if (_nonCoveredBranchInfo == null || _nonCoveredBranchInfo.Branches.Count < 1)
            {
                return;
            }
            foreach (var branch in _nonCoveredBranchInfo.Branches)
            {
                nonCoveredBranchTreeView.Nodes.Add(branch.ToString());
            }
        }

        private void UpdateCandidateObjectCreationProblemList()
        {
            candidateObjectProblemList.Items.Clear();

            if (_candidateObjectCreationProblems != null)
            {
                foreach (var candidateObjectCreationProblem in _candidateObjectCreationProblems)
                {
                    candidateObjectProblemList.Items.Add(candidateObjectCreationProblem);
                }
                candidateObjectProblemList.DisplayMember = "ShortDescription";
                candidateObjectCreationProblemCountLabel.Text = _candidateObjectCreationProblems.Count.ToString();
            }
        }

        private void UpdateUninstrumentedMethodList()
        {
            uninstrumentedMethodList.Items.Clear();

            if (unInstrumentedMethodNames != null)
            {
//                foreach (var name in unInstrumentedMethodNames)
//                {
//                    uninstrumentedMethodList.Items.Add(name);
//                }
                foreach (var name in unInstrumentedMethodNames)
                {
                    uninstrumentedMethodList.Items.Add(name);
                }
                uninstrumentedMethodCountLabel.Text = unInstrumentedMethodNames.Count.ToString();
            }
        }

        private void UpdateUninstrumentedMethodInBranchList()
        {
            uninstrumentedMethodInBranchList.Items.Clear();

            if (resultTrackingInfo.UnIntrumentedMethodInBranch != null)
            {
                //                foreach (var name in unInstrumentedMethodNames)
                //                {
                //                    uninstrumentedMethodList.Items.Add(name);
                //                }
                foreach (
                    KeyValuePair<string, HashSet<BranchInfo>> pair in resultTrackingInfo.UnIntrumentedMethodInBranch)
                {
                    uninstrumentedMethodInBranchList.Items.Add(pair);
                }
                uninstrumentedMethodInBranchList.DisplayMember = "Key";
                uninstrumentedMethodInBranchLabel.Text = resultTrackingInfo.UnIntrumentedMethodInBranch.Count.ToString();
            }
        }

        private void UpdateProblemTree()
        {
//            foreach (var branch in _nonCoveredBranchInfo.Branches)
//            {
//                IssueWithBranches.Add(branch, new HashSet<Issue>());
//            }

            AssignBranchesToProblems();

            problemTree.BeginUpdate();
            problemTree.Nodes.Clear();
            foreach (var problemWithBranche in ProblemWithBranches)
            {
                var problem = problemWithBranche.Key;


                var node = new TreeNode(problem.Kind + " : " + problem.Type);
                node.Tag = problem.Description;
                problemTree.Nodes.Add(node);
                foreach (var branch in problemWithBranche.Value)
                {
                    string text = branch.Document + ":" + branch.Line + ", ILOffset: " + branch.ILOffset;
                    var treeNode = new TreeNode(text);
                    treeNode.Tag = node.Tag;
                    node.Nodes.Add(treeNode);
                }
            }
            problemTree.EndUpdate();
            problemTree.ExpandAll();
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void LoadInfosFromFiles()
        {
            infoDirectory = "c:\\tempSeqex\\" + txtAssemblyName.Text + "\\";
            assemblyName = txtAssemblyName.Text;

            objectCreationProblemFileName = infoDirectory + assemblyName + ".object.bin";
            notCoveredBranchFileName = infoDirectory + assemblyName + ".assembly.bin";
            insufficientObjectFactoryFieldInfoFileName = infoDirectory + assemblyName + ".insufficientFields.bin";
            insufficientFieldInfoWithBranchInfoFileName = infoDirectory + assemblyName +
                                                          ".insufficientFields.branch.bin";
            resultTrackingInfoFileName = infoDirectory + assemblyName + ".resultTrack.bin";
            candidateObjectCreationProblemFileName = infoDirectory + assemblyName + ".candidateObjectCreationIssue.bin";
            uninstrumentedMethodProblemFileName = infoDirectory + assemblyName + ".uninstrumented.bin";
            foundUninstrumentedInObjectCreationFileName = infoDirectory + assemblyName + ".uninstrumentObj.bin";
            exceptionExternalMethodsFileName = infoDirectory + assemblyName + ".exceptionExternalMethods.bin";
            foundObjectTypeFileName = infoDirectory + assemblyName + ".foundObjectType.bin";

            insufficientObjectFactoryFieldInfo =
                (InsufficientObjectFactoryFieldInfo) LoadInfoFromFile(insufficientObjectFactoryFieldInfo,
                                                                      insufficientObjectFactoryFieldInfoFileName);
            _problemInfo =
                (ProblemInfo) LoadInfoFromFile(_problemInfo, objectCreationProblemFileName);
            _nonCoveredBranchInfo =
                (NonCoveredBranchInfo) LoadInfoFromFile(_nonCoveredBranchInfo, notCoveredBranchFileName);
            insufficientFieldInfoWithBranchInfo =
                (InsufficientFieldInfoWithBranchInfo) LoadInfoFromFile(insufficientObjectFactoryFieldInfo,
                                                                       insufficientFieldInfoWithBranchInfoFileName);
            resultTrackingInfo = (ResultTrackingInfo) LoadInfoFromFile(resultTrackingInfo, resultTrackingInfoFileName);

            unInstrumentedMethodNames =
                (List<string>) LoadInfoFromFile(unInstrumentedMethodNames, uninstrumentedMethodProblemFileName);

            _candidateObjectCreationProblems =
                (HashSet<CandidateObjectCreationProblem>) LoadInfoFromFile(_candidateObjectCreationProblems,
                                                                         candidateObjectCreationProblemFileName);

            uninstrumentedMethodsFoundInObj =
                (Dictionary<string, HashSet<BranchInfo>>) LoadInfoFromFile(uninstrumentedMethodsFoundInObj,
                                                                           foundUninstrumentedInObjectCreationFileName);

            ExceptionExternalMethods =
                (HashSet<ExceptionExternalMethod>)
                LoadInfoFromFile(ExceptionExternalMethods, exceptionExternalMethodsFileName);

            FoundTypes = (HashSet<string>) LoadInfoFromFile(FoundTypes, foundObjectTypeFileName);
            if (FoundTypes != null && FoundTypes.Contains("System.Object"))
            {
                FoundTypes.Remove("System.Object");
            }
        }

        private void AssignBranchesToProblems()
        {
//            foreach (var codeLocationToRelevantField in insufficientFieldInfoWithBranchInfo.CodeLocationToRelevantFields
//                )
//            {
//                var BranchInfo = codeLocationToRelevantField.Key;
//                if (_nonCoveredBranchInfo.Branches.Contains(BranchInfo))
//                {
//                    foreach (var field in codeLocationToRelevantField.Value)
//                    {
////                        var types = _issueInfo.ObjectCreationIssues[PexExplorableEventKind.GuessedClass];
////                        foreach (var type in types)
////                        {
////                            if (field.Contains(type))
////                            {
//                        IssueWithBranches[BranchInfo].Add(new Issue(IssueKind.ObjectCreation,
//                                                                   "object creation issue:" +
//                                                                   field));
////                            }
////                        }
//                    }
//                }
//            }
            ProblemWithBranches.Clear();
            foreach (var candidateObjectCreationProblem in _candidateObjectCreationProblems)
            {
                var branchInfo = candidateObjectCreationProblem.BranchLocation;
                if (_nonCoveredBranchInfo.BranchLocations.Contains(branchInfo.ToLocation))
                {
                    var problem = new Problem(ProblemKind.ObjectCreationProblem, candidateObjectCreationProblem.TargetType,
                                          candidateObjectCreationProblem.ToString());
                    if (!ProblemWithBranches.ContainsKey(problem))
                    {
                        ProblemWithBranches.Add(problem, new HashSet<BranchInfo>());
                    }

                    ProblemWithBranches[problem].Add(branchInfo);
                }

                if (candidateObjectCreationProblem.BranchLocation.Line == -1)
                {
                    if (candidateObjectCreationProblem.TargetObjectType != null)
                    {
                        var problem = new Problem(ProblemKind.ObjectCreationProblem, candidateObjectCreationProblem.TargetType,
                                              candidateObjectCreationProblem.ToString());
                        if (!ProblemWithBranches.ContainsKey(problem))
                        {
                            ProblemWithBranches.Add(problem, new HashSet<BranchInfo>());
                        }

                        ProblemWithBranches[problem].Add(branchInfo);
                    }
                }
            }

            foreach (KeyValuePair<Problem, HashSet<BranchInfo>> pair in ProblemWithBranches)
            {
                List<BranchInfo> needToRemoves = new List<BranchInfo>();
                if (pair.Value.Count > 1)
                {

                    foreach (BranchInfo info in pair.Value)
                    {
                        if (info.Line == -1)
                        {
                            needToRemoves.Add(info);
                        }
                    }
                }
                foreach (BranchInfo info in needToRemoves)
                {
                    pair.Value.Remove(info);
                }
            }


            foreach (KeyValuePair<string, HashSet<BranchInfo>> pair in resultTrackingInfo.UnIntrumentedMethodInBranch)
            {
                var methodName = pair.Key;
                var infos = pair.Value;
                foreach (var info in infos)
                {
                    if (_nonCoveredBranchInfo.Branches.Contains(info))
                    {
                        var problem = new Problem(ProblemKind.ExternalMethodProblem,
                                              methodName, "External method found in branch: " + methodName);

                        if (!ProblemWithBranches.ContainsKey(problem))
                        {
                            ProblemWithBranches.Add(problem, new HashSet<BranchInfo>());
                        }

                        ProblemWithBranches[problem].Add(info);
                    }
                }
            }


            foreach (var methodsFoundInObj in uninstrumentedMethodsFoundInObj)
            {
                var branchInfo = methodsFoundInObj.Value;

                string methodName = methodsFoundInObj.Key;
                var problem = new Problem(ProblemKind.ExternalMethodProblem,
                                      methodName, "External method found in objec creation: " + methodName);

                if (!ProblemWithBranches.ContainsKey(problem))
                {
                    ProblemWithBranches.Add(problem, new HashSet<BranchInfo>());
                }
                foreach (var info in branchInfo)
                {
                    ProblemWithBranches[problem].Add(info);
                }
            }

            foreach (var exceptionExternalMethod in ExceptionExternalMethods)
            {
                int line = 0;
                try
                {
                    line = Convert.ToInt32(exceptionExternalMethod.Line);
                }
                catch (Exception)
                {
                }
                var branchInfo = new BranchInfo("", line, 0, 0, exceptionExternalMethod.CallerMethod, 0);

                string methodName = exceptionExternalMethod.MethodName;
                if (methodName.Contains("Pex") || methodName.Contains("GetObjectData"))
                {
                    return;
                }
                var problem = new Problem(ProblemKind.ExternalMethodProblem,
                                      methodName,
                                      "External method found in exception:\n" +
                                      exceptionExternalMethod.ExceptionString);

                if (!ProblemWithBranches.ContainsKey(problem))
                {
                    ProblemWithBranches.Add(problem, new HashSet<BranchInfo>());
                }

                ProblemWithBranches[problem].Add(branchInfo);
            }
        }

        private
            void DumpInfoToDebugFile(string s, string fileName)
        {
            try
            {
                if (!
                    Directory.Exists(infoDirectory))
                    Directory.CreateDirectory(infoDirectory);
                TextWriter tw =
                    new
                        StreamWriter(fileName);
                tw.WriteLine(s);
                tw.Close();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        private void txtAssemblyName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button1_Click(null, null);
            }
        }

        private void branchTree_MouseMove(object sender, MouseEventArgs e)
        {
//            // Get the node at the current mouse pointer location.
//            TreeNode theNode = this.issueTree.GetNodeAt(e.X, e.Y);
//
//            // Set a ToolTip only if the mouse pointer is actually paused on a node.
//            if ((theNode != null))
//            {
//                // Verify that the tag property is not "null".
//                if (theNode.Tag != null)
//                {
//                    branchDetail.Text = (string) theNode.Tag;
//                }
//            }
        }

        private void uninstrumentedMethodList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (uninstrumentedMethodList.SelectedItem != null)
            {
                branchDetail.Text = uninstrumentedMethodList.SelectedItem.ToString();
            }
        }

        private void candidateObjectProblemList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (candidateObjectProblemList.SelectedItem != null)
            {
                branchDetail.Text =
                    ((CandidateObjectCreationProblem) (candidateObjectProblemList.SelectedItem)).ToString();
            }
        }

        private void uninstrumentedMethodInBranchList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (uninstrumentedMethodInBranchList.SelectedItem != null)
            {
                var pair = (KeyValuePair<string, HashSet<BranchInfo>>) uninstrumentedMethodInBranchList.SelectedItem;
                string text = "============External Method Found In The Constraints of Branch======================\n" +
                              pair.Key + "\nBranch: ";
                foreach (BranchInfo info in pair.Value)
                {
                    text += info.ToString();
                }
                branchDetail.Text = text;
            }
        }

        private void label4_Click(object sender, EventArgs e)
        {
        }

        private void problemTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
        }

        private void problemTree_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            // Get the node at the current mouse pointer location.
            TreeNode theNode = this.problemTree.GetNodeAt(e.X, e.Y);

            // Set a ToolTip only if the mouse pointer is actually paused on a node.
            if ((theNode != null))
            {
                // Verify that the tag property is not "null".
                if (theNode.Tag != null)
                {
                    branchDetail.Text = (string) theNode.Tag;
                }
            }
        }

        private void label14_Click(object sender, EventArgs e)
        {
        }
    }
}