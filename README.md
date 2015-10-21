
Covana: Precise Identification of Problems for Structural Test Generation 
==========================================================================



Project Summary:
The process of achieving high structural coverage of the program under test can be automated using tools built for automated test-generation approaches. When applied on complex programs in practice, these approaches face various problems in generating test inputs to achieve high structural coverage. Our preliminary study identified two main types of problems: (1) external-method-call problem (EMCP), where the method calls from external libraries throw exceptions to abort test executions or their return values are used to decide subsequent branches, causing the branches not to be covered; (2) object-creation problem (OCP), where tools fails to generate method-call sequences to produce desirable object states. Since these automated tools could not be powerful enough to deal with these various problems in testing complex programs, we advocate cooperative developer testing, where developers provide guidance to help tools achieve higher structural coverage. To reduce the efforts of developers in providing guidance to tools, in this paper, we propose a novel approach, called Covana, to precisely identify and report problems that prevent the tools from achieving high structural coverage by computing data dependency between problem candidates and not-covered branches. We provide two techniques to instantiate Covana to identify EMCPs and OCPs. To show the effectiveness of Covana, we conduct evaluations on two open source projects. Our results show that Covana effectively identifies 43 EMCPs out of 1610 EMCP candidates with only 1 false positive and 2 false negative, and 155 OCPs out of 451 OCP candidates with 20 false positives and 28 false negatives.

Project website:
http://research.csc.ncsu.edu/ase/projects/covana/

This release version includes some simple examples that demonstrate Covana's effectiveness. The examples can be found under the Benchmarks project of the Covana solution.

* Instruction on how to use Covana for detecting problems for the example programs

1. Select one of the examples under project Benchmarks
2. Right click the class or method and then select "Run Pex Explorations"
3. After Pex finish exploration, launch the project "CovanaAnalysisForm". A main window of Covana will show up.
4. By default, the textfield of "AssemblyName" is filled with "Benchmarks", which is the name of the assembly for the example projects.
5. Click the "Analyse" button to get result.

* Instruction on how to use Covana for detecting problems in test programs

1. Install Pex
2. Add Covana project into your project reference
3. Add the following 5 properties in the AssemblyInfo.cs of your test project:
[assembly: IssueTrack]
[assembly: ProblemObserver]
[assembly: IssueObserver]
[assembly: AssemblyCoverageObserver]
[assembly: ResultTrackingObserver]
4. Do from the step 2 of the instructions on how to use Covana for example projects


This is prototype implemented for our Covana approach and and is released under the following license:

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

Please contact Xusheng Xiao (xxiao2@ncsu.edu) for any doubts or suggestions.

# ICSE-2011-COVANA
This repository contains information related to the tool Covana . Covana was developed as an extension to Pex- A tool for automation in developer testing.

The tool was originally presented in this [Paper](https://people.engr.ncsu.edu/txie/publications/icse11demo-covana.pdf) at International Conference on Software Engineering, 2011.

Please note that this repository is not the original repository for this tool. This repository is merely for hosting the tool on GitHub and [I](https://github.com/smallen3) am not the original author of this tool.

Here is the link to the [Original Project Page](https://research.csc.ncsu.edu/ase/projects/covana/)
Here is the link to the [Video](https://research.csc.ncsu.edu/ase/projects/covana/covana.html) showing the demonstration of the tool.

In this repository, for Covana you will find:

 :white_check_mark: Source code for Covana
 
 :white_check_mark: Executable for Pex, which is required for Covana to run 
 
 :white_check_mark: [The original page for Download](https://pexase.codeplex.com/) 


This repository was constructed by [Sai Sindhur Malleni](https://github.com/smallen3) of Team New Hanover under the supervision of [Dr. Emerson Murphy-Hill](https://github.com/CaptainEmerson).

Thanks to the authors Xusheng Xiao, Tao Xie, Nikolai Tillmann, and Peli de Halleux for making the tool available. 

