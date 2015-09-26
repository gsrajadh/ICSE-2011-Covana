using System.Windows.Forms;

namespace CoverageAnalysisForm
{
    partial class CovanaForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label2 = new System.Windows.Forms.Label();
            this.button1 = new System.Windows.Forms.Button();
            this.problemTree = new System.Windows.Forms.TreeView();
            this.txtAssemblyName = new System.Windows.Forms.TextBox();
            this.branchDetail = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.uninstrumentedMethodList = new System.Windows.Forms.ListBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.candidateObjectProblemList = new System.Windows.Forms.ListBox();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.uninstrumentedMethodCountLabel = new System.Windows.Forms.Label();
            this.candidateObjectCreationProblemCountLabel = new System.Windows.Forms.Label();
            this.uninstrumentedMethodInBranchLabel = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.uninstrumentedMethodInBranchList = new System.Windows.Forms.ListBox();
            this.nonCoveredBranchTreeView = new System.Windows.Forms.TreeView();
            this.label9 = new System.Windows.Forms.Label();
            this.foundObjectTypeCountlabel = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            this.foundObjectTypeList = new System.Windows.Forms.ListBox();
            this.SuspendLayout();
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(42, 29);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(79, 13);
            this.label2.TabIndex = 1;
            this.label2.Text = "AssemblyName";
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(813, 43);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 2;
            this.button1.Text = "Analyse!";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // issueTree
            // 
            this.problemTree.Location = new System.Drawing.Point(45, 264);
            this.problemTree.Name = "issueTree";
            this.problemTree.Size = new System.Drawing.Size(450, 191);
            this.problemTree.TabIndex = 4;
            this.problemTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.problemTree_AfterSelect);
            this.problemTree.NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.problemTree_NodeMouseClick);
            this.problemTree.MouseMove += new System.Windows.Forms.MouseEventHandler(this.branchTree_MouseMove);
            // 
            // txtAssemblyName
            // 
            this.txtAssemblyName.Location = new System.Drawing.Point(45, 45);
            this.txtAssemblyName.Name = "txtAssemblyName";
            this.txtAssemblyName.Size = new System.Drawing.Size(149, 20);
            this.txtAssemblyName.TabIndex = 6;
            this.txtAssemblyName.Text = "Benchmarks";
            this.txtAssemblyName.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtAssemblyName_KeyDown);
            // 
            // branchDetail
            // 
            this.branchDetail.Location = new System.Drawing.Point(45, 102);
            this.branchDetail.Multiline = true;
            this.branchDetail.Name = "branchDetail";
            this.branchDetail.ReadOnly = true;
            this.branchDetail.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.branchDetail.Size = new System.Drawing.Size(450, 143);
            this.branchDetail.TabIndex = 8;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(42, 86);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(83, 13);
            this.label3.TabIndex = 9;
            this.label3.Text = "Analysis Details:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(42, 248);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(220, 13);
            this.label4.TabIndex = 10;
            this.label4.Text = "Problems detected for not-covered branches:";
            this.label4.Click += new System.EventHandler(this.label4_Click);
            // 
            // uninstrumentedMethodList
            // 
            this.uninstrumentedMethodList.FormattingEnabled = true;
            this.uninstrumentedMethodList.Location = new System.Drawing.Point(513, 102);
            this.uninstrumentedMethodList.Name = "uninstrumentedMethodList";
            this.uninstrumentedMethodList.Size = new System.Drawing.Size(375, 121);
            this.uninstrumentedMethodList.TabIndex = 11;
            this.uninstrumentedMethodList.SelectedIndexChanged += new System.EventHandler(this.uninstrumentedMethodList_SelectedIndexChanged);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(510, 86);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(245, 13);
            this.label5.TabIndex = 12;
            this.label5.Text = "External Methods Encountered During Exploration:";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(510, 384);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(180, 13);
            this.label6.TabIndex = 14;
            this.label6.Text = "Candidate Object Creation Problems:";
            // 
            // candidateObjectIssueList
            // 
            this.candidateObjectProblemList.FormattingEnabled = true;
            this.candidateObjectProblemList.Location = new System.Drawing.Point(513, 400);
            this.candidateObjectProblemList.Name = "candidateObjectIssueList";
            this.candidateObjectProblemList.Size = new System.Drawing.Size(375, 147);
            this.candidateObjectProblemList.TabIndex = 13;
            this.candidateObjectProblemList.SelectedIndexChanged += new System.EventHandler(this.candidateObjectProblemList_SelectedIndexChanged);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(836, 86);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(38, 13);
            this.label7.TabIndex = 15;
            this.label7.Text = "Count:";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(836, 384);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(38, 13);
            this.label8.TabIndex = 16;
            this.label8.Text = "Count:";
            // 
            // uninstrumentedMethodCountLabel
            // 
            this.uninstrumentedMethodCountLabel.AutoSize = true;
            this.uninstrumentedMethodCountLabel.Location = new System.Drawing.Point(875, 86);
            this.uninstrumentedMethodCountLabel.Name = "uninstrumentedMethodCountLabel";
            this.uninstrumentedMethodCountLabel.Size = new System.Drawing.Size(13, 13);
            this.uninstrumentedMethodCountLabel.TabIndex = 17;
            this.uninstrumentedMethodCountLabel.Text = "0";
            // 
            // candidateObjectCreationIssueCountLabel
            // 
            this.candidateObjectCreationProblemCountLabel.AutoSize = true;
            this.candidateObjectCreationProblemCountLabel.Location = new System.Drawing.Point(875, 384);
            this.candidateObjectCreationProblemCountLabel.Name = "candidateObjectCreationIssueCountLabel";
            this.candidateObjectCreationProblemCountLabel.Size = new System.Drawing.Size(13, 13);
            this.candidateObjectCreationProblemCountLabel.TabIndex = 18;
            this.candidateObjectCreationProblemCountLabel.Text = "0";
            // 
            // uninstrumentedMethodInBranchLabel
            // 
            this.uninstrumentedMethodInBranchLabel.AutoSize = true;
            this.uninstrumentedMethodInBranchLabel.Location = new System.Drawing.Point(875, 234);
            this.uninstrumentedMethodInBranchLabel.Name = "uninstrumentedMethodInBranchLabel";
            this.uninstrumentedMethodInBranchLabel.Size = new System.Drawing.Size(13, 13);
            this.uninstrumentedMethodInBranchLabel.TabIndex = 22;
            this.uninstrumentedMethodInBranchLabel.Text = "0";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(836, 234);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(38, 13);
            this.label10.TabIndex = 21;
            this.label10.Text = "Count:";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(510, 234);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(290, 13);
            this.label11.TabIndex = 20;
            this.label11.Text = "External Methods linked to Branches via Data Dependency:";
            // 
            // uninstrumentedMethodInBranchList
            // 
            this.uninstrumentedMethodInBranchList.FormattingEnabled = true;
            this.uninstrumentedMethodInBranchList.Location = new System.Drawing.Point(513, 250);
            this.uninstrumentedMethodInBranchList.Name = "uninstrumentedMethodInBranchList";
            this.uninstrumentedMethodInBranchList.Size = new System.Drawing.Size(375, 121);
            this.uninstrumentedMethodInBranchList.TabIndex = 19;
            this.uninstrumentedMethodInBranchList.SelectedIndexChanged += new System.EventHandler(this.uninstrumentedMethodInBranchList_SelectedIndexChanged);
            // 
            // nonCoveredBranchTreeView
            // 
            this.nonCoveredBranchTreeView.Location = new System.Drawing.Point(45, 474);
            this.nonCoveredBranchTreeView.Name = "nonCoveredBranchTreeView";
            this.nonCoveredBranchTreeView.Size = new System.Drawing.Size(450, 243);
            this.nonCoveredBranchTreeView.TabIndex = 23;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(42, 458);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(116, 13);
            this.label9.TabIndex = 24;
            this.label9.Text = "Not-covered branches:";
            // 
            // foundObjectTypeCountlabel
            // 
            this.foundObjectTypeCountlabel.AutoSize = true;
            this.foundObjectTypeCountlabel.Location = new System.Drawing.Point(875, 554);
            this.foundObjectTypeCountlabel.Name = "foundObjectTypeCountlabel";
            this.foundObjectTypeCountlabel.Size = new System.Drawing.Size(13, 13);
            this.foundObjectTypeCountlabel.TabIndex = 28;
            this.foundObjectTypeCountlabel.Text = "0";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(836, 554);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(38, 13);
            this.label13.TabIndex = 27;
            this.label13.Text = "Count:";
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(510, 554);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(164, 13);
            this.label14.TabIndex = 26;
            this.label14.Text = "Candidate Object Types for OCP:";
            this.label14.Click += new System.EventHandler(this.label14_Click);
            // 
            // foundObjectTypeList
            // 
            this.foundObjectTypeList.FormattingEnabled = true;
            this.foundObjectTypeList.Location = new System.Drawing.Point(513, 570);
            this.foundObjectTypeList.Name = "foundObjectTypeList";
            this.foundObjectTypeList.Size = new System.Drawing.Size(375, 147);
            this.foundObjectTypeList.TabIndex = 25;
            // 
            // CovanaForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 729);
            this.Controls.Add(this.foundObjectTypeCountlabel);
            this.Controls.Add(this.label13);
            this.Controls.Add(this.label14);
            this.Controls.Add(this.foundObjectTypeList);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.nonCoveredBranchTreeView);
            this.Controls.Add(this.uninstrumentedMethodInBranchLabel);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.uninstrumentedMethodInBranchList);
            this.Controls.Add(this.candidateObjectCreationProblemCountLabel);
            this.Controls.Add(this.uninstrumentedMethodCountLabel);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.candidateObjectProblemList);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.uninstrumentedMethodList);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.branchDetail);
            this.Controls.Add(this.txtAssemblyName);
            this.Controls.Add(this.problemTree);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.label2);
            this.Name = "CovanaForm";
            this.Text = "CovanaForm";
            this.Load += new System.EventHandler(this.CovanaForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.TreeView problemTree;
        private System.Windows.Forms.TextBox txtAssemblyName;
        private TextBox branchDetail;
        private Label label3;
        private Label label4;
        private ListBox uninstrumentedMethodList;
        private Label label5;
        private Label label6;
        private ListBox candidateObjectProblemList;
        private Label label7;
        private Label label8;
        private Label uninstrumentedMethodCountLabel;
        private Label candidateObjectCreationProblemCountLabel;
        private Label uninstrumentedMethodInBranchLabel;
        private Label label10;
        private Label label11;
        private ListBox uninstrumentedMethodInBranchList;
        private TreeView nonCoveredBranchTreeView;
        private Label label9;
        private Label foundObjectTypeCountlabel;
        private Label label13;
        private Label label14;
        private ListBox foundObjectTypeList;
    }
}