
use chilkat;


if(@ARGV == 0)
{
	die("Usage: Perl DFORD.pl <Command file name> <optMaxLimit>\n\n");
}


my($bUseMaxLimits) = 0;
if(@ARGV == 2 && $ARGV[1] == 1)
{
	$bUseMaxLimits = 1;
}

#Constructing global variables
$TOTAL_MAX_ATTEMPTS = 100;
$STORAGE_DIR = "c:\\tempSeqEx";
$factorystore = $STORAGE_DIR."\\FactoryStore.bin";
$statusfile = $STORAGE_DIR."\\reexecute.txt";
$report_path = "PexMeOp";
$nesteddepth = 0;

@allExecutedCommands = ();
my($globstarttime) = (time)[0];
open (OUTPUT, ">Timing.txt");
&executeCMDFile($ARGV[0]);
close OUTPUT;

my($globendtime) = (time)[0];
print "Total elapsed time for all PUTs ".($globendtime - $globstarttime)." seconds \n";
print "DONE!!!";

###################################### BEGIN OF SUBROUTINES #######################################

#Executes a command file supplied as input
sub executeCMDFile()
{
	my($cmdfile) = @_;
	open CMDFILE, $cmdfile or die "ERROR: Could not open command file!!!\n";
	my(@allCommands) = <CMDFILE>;
	close CMDFILE;

	$ENV{"PEXME_NESTED_DEPTH"} = $nesteddepth;

	#Execute each command from the command file
	foreach(@allCommands)
	{
		my($cmd) = $_;
		chomp($cmd);	
		
		#Ignore comments in the command file
		if($cmd =~ /^\#/ || $cmd eq "")
		{
			next;
		}
		$cmd = $cmd." /ro:".$report_path;

		#Check whether the command is executed earlier due to NESTEDEXECUTE that
		#can occur from the Pex process
		my($alreadyexecuted) = 0;
		foreach(@allExecutedCommands)
		{
			my($local_loop_cmd) = $_;
			if($local_loop_cmd eq $cmd)
			{
				$alreadyexecuted = 1;
				last;
			}
		}

		if($alreadyexecuted == 1)
		{
			print "\nPERL: Command ".$cmd." already executed earlier. Proceeding with the next command!!!\n\n";
			next;
		}
		
		&executePUT($cmd);
	}
}

#Executes a PUT
sub executePUT()
{
	my($command) = @_;
	my($count) = 1;
	my($reportname) = "";

	push (@allExecutedCommands, $command);

	#Getting the report name from the commands
	if($command =~ /^(.*)(\/rn:)([^ ]+)([ ])(.*)$/)
	{
		$reportname = $3;
	}

	my($assemblyname) = "default";
	if($command =~ /^(.*)(\\)([^\\]*.dll)(.*)$/)
	{
		$assemblyname = $3;
	}	

	my($rdir) =	$report_path."\\".$reportname;
	my($maxrunswithoutnewtests) = 100;
	my($maxruns) = 200;
	my($timeout) = 120;
	my($constsolvertime) = 2;

	if($bUseMaxLimits == 1)
	{
		$maxrunswithoutnewtests = 2147483647;
		$maxruns = 2147483647;
		$timeout = 500;
		$constsolvertime = 10;
	}

	my($starttime) = (time)[0];

	#Delete factory suggestion store at the beginning of the PUT
	#if($nesteddepth == 0)
	#{
	#	system("D:\\PexASE\\Projects\\SeqEx\\PexMeCleaner\\bin\\Debug\\PexMeHelper.exe Clean");
	#}	

	while ($count < $TOTAL_MAX_ATTEMPTS)
	{
		#Delete the reports folder if exists
		#system("rmdir /Q /S ".$rdir);

		my($localcommand) = $command." /MaxRuns:".$maxruns." /MaxRunsWithoutNewTests:".$maxrunswithoutnewtests." /Timeout:".$timeout." /MaxConstraintSolverTime:".$constsolvertime;
		system($localcommand);
	 
        if(!(-e $statusfile))
		{
			#If the status file is missing
			print "Status file missing, copying current tests and continuinig with next PUT";
			&copyTestCases($rdir, $STORAGE_DIR."\\".$assemblyname);
			last;
		}

		#Check whether re-execution is required.
		open FILE, $statusfile or die "Couldn't status file\n";
		binmode FILE;
		my(@statusarr) = <FILE>;
		close FILE;
	
		my($statusstr) = @statusarr[0];
		chomp($statusstr);

		if($statusstr =~ /NESTEDEXECUTE/)
		{
			print "Requested NESTEDEXECUTE from the Pex process\n";
			print "*********************************************** Restarting with new process\n\n";
			$nesteddepth++;
			$ENV{"PEXME_NESTED_DEPTH"} = $nesteddepth;
			chomp(@statusarr[1]);
			&executeCMDFile(@statusarr[1]);
			my($starttime) = (time)[0];		#Resetting the start time, if not start time accumulates the entire time from other process
			$nesteddepth--;
			$ENV{"PEXME_NESTED_DEPTH"} = $nesteddepth;
		}

		&copyTestCases($rdir, $STORAGE_DIR."\\".$assemblyname);
		if($statusstr =~ /STOP/)
		{
			print "Requested STOP from the Pex process\n";			
			last;
		}

		if($statusstr =~ /REEXECUTE/)
		{
			print "Received REEXECUTE command. So, relauching the Pex process\n";
		}

		$count++;
		$maxrunswithoutnewtests = $maxrunswithoutnewtests + 50;
		$maxruns = $maxruns + 100;
		$timeout = $timeout + 60;
		$constsolvertime = $constsolvertime + 1;

		#delete the status file for the next run
		if(-e $statusfile)
		{
			system(" del ".$statusfile);
		}
	}

	my($endtime) = (time)[0];
	print OUTPUT "Elapsed time for ".$command.":  ".($endtime - $starttime)." seconds \n";

}

#Subroutine for copying a directory into another. Instead of copying the entire directory, this method copies just the .cs files
sub copyTestCases()
{
	my($src, $dest) = @_;

	if(!(-d $dest))
	{
		system("mkdir ".$dest);
	}
	#my($cmd) = "robocopy /MOV /E ".$src." ".$dest;
	#system($cmd);	
	
	$dirTree = new chilkat::CkDirTree();

	#  Specify the root of the directory tree to be traversed.
	$dirTree->put_BaseDir($src);

	#  Indicate that we want to recursively traverse the tree.
	$dirTree->put_Recurse(1);

	#  Begin the directory tree traversal.
	$success = $dirTree->BeginIterate();
	if ($success != 1) {
	    print $dirTree->lastErrorText() . "\r\n";
		return;
	}

	while ($dirTree->get_DoneIterating() != 1) {	
		#found a file
		if ($dirTree->get_IsDirectory() != 1) {
			$absfname = $dirTree->fullPath();
			if($absfname =~ /(.*)(\\)([^.]*)(.g.cs)$/)
			{
				$dirname = $1;
				$fnamewithoutext = $3;

				$currTime = (time)[0];
				$compfilename = $dest."\\".$fnamewithoutext.$currTime.".g.cs";				
				if(length($compfilename) > 250)
				{
					#TODO: Needs to handle this case

				}

				#Generate a unique filename always
				while(-e $compfilename)
				{
					$currTime = $currTime + 1;
					$compfilename = $dest."\\".$fnamewithoutext.$currTime.".g.cs";
				}

				&copyAndreplClassName($absfname, $compfilename, $currTime);
			}
		}

	    #  Advance to the next file or sub-directory in the tree traversal.
	    $success = $dirTree->AdvancePosition();
	    if ($success != 1) {
		    if ($dirTree->get_DoneIterating() != 1) {
			    print $dirTree->lastErrorText() . "\r\n";
				return;
	        }
		}
	}
}

#Copies file and also replaces the classname within the file
sub copyAndreplClassName()
{
	my($srcfile, $destfile, $currTime) = @_;
	my($cmd) = "PexMeHelper CopyNChgClass ".$srcfile." ".$destfile." ".$currTime;
	system($cmd);
}