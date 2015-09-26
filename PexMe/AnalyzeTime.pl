#Open the timing file
open TIMEFILE, $ARGV[0] or die "ERROR: Could not open timing file!!!\n";
my(@allentries) = <TIMEFILE>;
close TIMEFILE;

$completime = 0;
foreach $line (@allentries) {
	chomp($line);
	chomp($line);
	if($line =~ /^(.*)(ro:PexMeOp:  )([^ ]*)( seconds)(.*)$/)
	{
		$timetaken = $3;
		$completime = $completime + $timetaken;
	}
}

print "Total time taken: ".$completime;