Author: Blake McCollough
Contact: blakemccollough@yahoo.com
Description:
	ServerLogDisplay.exe takes QS/1 server logs as input and displays every network signon and signoff. The purpose is to help
	visualize network useage at a specific time. Input can be given and read as a .log file, or opened automatically
	from a given network. In order to open from a network automatically, a customer ID and date must be given, to help narrow down
	results. Once a network opens, every network signon and signoff is displayed as a spreadsheet. Clicking on a list item
	will display the entire duration of a network task (if a task starts before the given time interval, the earliest timestamp
	in the file is used as the start time). The textboxes under each column act as filterers. The graph view tab visualizes the
	total number of tasks being used throughout the duration of the QS1 files, where each day is seperates by the background color.
	Finally, the date may be exported as a .CSV file. If two or more rows are selected, only that selection will be exported.
	Otherwise, the entire table will be exported.
	
	NOTE: If not every task is signed off before being signed on again in sequential order in the input file, task information
	may not be fully accurate. A warning will be displayed at the bottom if this is the case.