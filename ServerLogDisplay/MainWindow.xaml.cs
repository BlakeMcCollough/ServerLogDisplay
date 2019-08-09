using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;

namespace ServerLogDisplay
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private StreamReader infile;
        private List<LogLine> LineList; //stores and keeps all records read from infile
        private ServerSearchBox SearchServerDialog;
        private double GRAPH_MARGIN;

        public MainWindow()
        {
            InitializeComponent();
            LineList = new List<LogLine>();
            GRAPH_MARGIN = 50;

            AutoLoadFromArgs(Environment.GetCommandLineArgs());
        }

        private void AutoLoadFromArgs(string[] format)
        {
            if(format.Length > 1 && Regex.IsMatch(format[1], @"^\w{4}\d{6}$") == true) //arg must be read as [network][yyMMdd]
            {
                OpenLogAtDate(format[1].Substring(0, 4), format[1].Substring(4));
            }
        }

        private LogLine GetPreviousUsedTask(string tsk)
        {
            if(LineList.Count <= 0)
            {
                return null;
            }
            for (int i = LineList.Count - 1; i >= 0; i--)
            {
                if (LineList[i].Task == tsk)
                {
                    return LineList[i];
                }
            }

            return null;
        }


        private string ForceSignoffString(string time, string task)
        {
            string fakeLine = time + @"00.000|=00000000| T";
            string decTask = "0" + int.Parse(task).ToString("x");
            if(decTask.Length < 3)
            {
                decTask = "0" + decTask;
            }
            fakeLine = fakeLine + decTask + @"| 00000000 00000000 00000000 Network Close: DDB cmd&error NCB                 " + GetPreviousUsedTask(task).Name;
            return fakeLine;
        }

        //reads through file line-by-line and stores line to signon or signoff query
        private void GatherSignOnOffs()
        {
            const int END_OF_DATE = 11;
            const int STATUSON = 1, STATUSOFF = 2, STATUSERR = 0;

            MainList.ItemsSource = null;
            Regex signonAcceptRegex = new Regex(@"\|=42000AA0\|");
            Regex signoffAcceptRegex = new Regex(@"\|=423000E0\|");
            Regex signerrorAcceptRegex = new Regex(@"\|=C23000E0\||\|=C2100140\|");
            Regex exitstepAcceptRegex = new Regex(@"\|=430004C0\|");
            bool passedExitStep = false;
            LineList.Clear();
            

            //instantiate graph object
            UsageGraph myGraph = new UsageGraph(GRAPH_MARGIN, canGraph.Width - GRAPH_MARGIN, GRAPH_MARGIN, canGraph.Height - GRAPH_MARGIN, canGraph.Width, canGraph.Height, GRAPH_MARGIN);
            MoveDot(0, 0, 0);
            canGraph.Children.Clear();
            canGraph.Children.Add(myGraph.getXaxis());
            canGraph.Children.Add(myGraph.getYaxis());
            canGraph.Children.Add(myGraph.getLine());

            List<LogLine> totalTaskage = new List<LogLine>(); //this keeps track of tasks currently running
            Dictionary<string, int> taskagePerDate = new Dictionary<string, int>();

            string line = infile.ReadLine();
            while(line != null)
            {
                if(signonAcceptRegex.IsMatch(line)) //someone has signed on
                {
                    LogLine newListItem = new LogLine(line, STATUSON);
                    LineList.Add(newListItem);
                    totalTaskage.Add(newListItem);
                    taskagePerDate[line.Substring(0, END_OF_DATE)] = totalTaskage.Count;
                }
                else if(signoffAcceptRegex.IsMatch(line)) //someone has signed off
                {
                    LogLine newListItem = new LogLine(line, STATUSOFF);
                    try
                    {
                        newListItem.IP = GetPreviousUsedTask(newListItem.Task).IP;
                    }
                    catch
                    {
                        newListItem.IP = "";
                    }
                    LineList.Add(newListItem);
                    totalTaskage.RemoveAll(x => x.Task == newListItem.Task);
                    taskagePerDate[line.Substring(0, END_OF_DATE)] = totalTaskage.Count;
                }
                else if(signerrorAcceptRegex.IsMatch(line))
                {
                    LogLine newListItem = new LogLine(line, STATUSERR);
                    LogLine prevTask = GetPreviousUsedTask(newListItem.Task);
                    if(prevTask != null && newListItem.Status != "Disconnect Error: -1")
                    {
                        newListItem.IP = prevTask.IP;
                        LineList.Add(newListItem);
                        totalTaskage.RemoveAll(x => x.Task == newListItem.Task);
                        taskagePerDate[line.Substring(0, END_OF_DATE)] = totalTaskage.Count;
                    }
                }
                else if(exitstepAcceptRegex.IsMatch(line))
                {
                    passedExitStep = true;
                }
                else if(line == "NEWFILEBREAK" && taskagePerDate.Count > 0)
                {
                    string myTime = taskagePerDate.LastOrDefault().Key;
                    while(totalTaskage.Count > 0)
                    {
                        LogLine newListItem = new LogLine(ForceSignoffString(myTime, totalTaskage[0].Task), STATUSOFF);
                        try
                        {
                            newListItem.IP = GetPreviousUsedTask(newListItem.Task).IP;
                        }
                        catch
                        {
                            newListItem.IP = "";
                        }
                        LineList.Add(newListItem);
                        totalTaskage.RemoveAt(0);
                    }
                    taskagePerDate[myTime] = totalTaskage.Count;
                    if(passedExitStep == false)
                    {
                        StartLabel.Text = "";
                        EndLabel.Text = "Exit steps not properly executed. Log information may not be correct";
                    }
                    passedExitStep = false;
                }


                line = infile.ReadLine();
            }
            if(LineList.Count <= 0)
            {
                MessageBox.Show("No acceptable data inputs");
                Tabs.IsEnabled = false;
                return;
            }
            if (passedExitStep == false)
            {
                StartLabel.Text = "";
                EndLabel.Text = "Exit steps not properly executed. Log information may not be correct";
            }

            MainList.ItemsSource = LineList;
            canGraph.Width = myGraph.CreateGraph(taskagePerDate); //puts the line on the graph and returns its width

            //not sure how to add a list of UIelements to canGraph children, so iterate through each instead
            List<UIElement> labelList = myGraph.labelList;
            foreach(UIElement label in labelList)
            {
                canGraph.Children.Add(label);
            }
            canGraph.Children.Add(dot);
            canGraph.Children.Add(taskageText);

            //seperate days using a canvas object iterating with daywidth
            DrawGraphBackground(myGraph, taskagePerDate.Keys.First());
            
        }

        private void DrawGraphBackground(UsageGraph myGraph, string iniStr)
        {
            const double DAY_WIDTH = 1440;
            SolidColorBrush bgColor1 = new SolidColorBrush(Color.FromRgb(205, 237, 255));
            SolidColorBrush bgColor2 = new SolidColorBrush(Color.FromRgb(241, 241, 252));

            double iniWidth = myGraph.initialWidth;
            if (iniWidth <= 0)
            {
                iniWidth = canGraph.Width; //iniwidth never makes it past midnight, set the entire graph to one color
            }
            DateTime startingDay = new DateTime(Int32.Parse("20" + iniStr.Substring(0, 2)), Int32.Parse(iniStr.Substring(2, 2)), Int32.Parse(iniStr.Substring(4, 2)), Int32.Parse(iniStr.Substring(7, 2)), Int32.Parse(iniStr.Substring(9, 2)), 0);
            Canvas iniLayout = new Canvas();
            iniLayout.Height = canGraph.Height;
            iniLayout.Width = iniWidth;
            iniLayout.Margin = new Thickness(0, 0, 0, 0);
            iniLayout.Background = bgColor1;
            Canvas.SetZIndex(iniLayout, -1);
            TextBlock firstDayLabel = new TextBlock();
            firstDayLabel.Text = startingDay.ToString("MM/dd/yyyy");
            firstDayLabel.Foreground = Brushes.Black;
            Canvas.SetLeft(firstDayLabel, iniLayout.Width / 2);
            Canvas.SetBottom(firstDayLabel, GRAPH_MARGIN - 0.8 * GRAPH_MARGIN);
            iniLayout.Children.Add(firstDayLabel);
            double offset = iniWidth;
            int i = 0;
            while (canGraph.Width > offset)
            {
                Canvas bgLayout = new Canvas();
                bgLayout.Height = canGraph.Height;
                if (offset + DAY_WIDTH > canGraph.Width) //graph won't end evenly on midnight, set to latest possible time
                {
                    bgLayout.Width = canGraph.Width - (offset);
                }
                else
                {
                    bgLayout.Width = DAY_WIDTH;
                }
                bgLayout.Margin = new Thickness(offset, 0, 0, 0);
                if (i % 2 == 0)
                {
                    bgLayout.Background = bgColor2;
                }
                else
                {
                    bgLayout.Background = bgColor1;
                }
                Canvas.SetZIndex(bgLayout, -1);
                TextBlock dayLabel = new TextBlock();
                dayLabel.Text = startingDay.AddDays(i + 1).ToString("MM/dd/yyyy");
                dayLabel.Foreground = Brushes.Black;
                Canvas.SetLeft(dayLabel, bgLayout.Width / 2);
                Canvas.SetBottom(dayLabel, GRAPH_MARGIN - 0.8 * GRAPH_MARGIN);
                bgLayout.Children.Add(dayLabel);

                offset = offset + DAY_WIDTH;
                i = i + 1;
                canGraph.Children.Add(bgLayout);
            }
            iniLayout.Width = iniLayout.Width + 100;
            canGraph.Children.Add(iniLayout);
        }

        //opens the file
        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog newFileWindow = new OpenFileDialog();
            if (newFileWindow.ShowDialog() == true)
            {
                Tabs.IsEnabled = true;
                infile = new StreamReader(newFileWindow.FileName);
                GatherSignOnOffs();

                infile.Close();
            }
        }

        //erases previous text when textbox is clicked
        private void Search_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox searchBox = e.Source as TextBox;
            searchBox.Text = "";
        }

        //just checks if any textbox has been filled by user and filters using regex: false meaning row is bad and true meaning row is good (made an if-ladder for better readability)
        private bool FilterList(LogLine row)
        {
            if(Regex.IsMatch(row.TimeStamp, "^" + RemoveSpecialCharacters(TimeSearch.Text)) == false)
            {
                return false;
            }
            if (Regex.IsMatch(row.Status, RemoveSpecialCharacters(StatusSearch.Text)) == false)
            {
                return false;
            }
            if (Regex.IsMatch(row.Task, "^" + RemoveSpecialCharacters(TaskSearch.Text) + "$") == false && String.IsNullOrEmpty(TaskSearch.Text) == false)
            {
                return false;
            }
            if (Regex.IsMatch(row.IP, "^" + RemoveSpecialCharacters(IpSearch.Text)) == false)
            {
                return false;
            }
            if (Regex.IsMatch(row.Name, "^" + RemoveSpecialCharacters(NameSearch.Text)) == false)
            {
                return false;
            }
            return true;
        }

        private string RemoveSpecialCharacters(string s)
        {
            return Regex.Replace(s, @"[^\w\s\/\.\-\:]", String.Empty);
        }

        //filters main list based on FilterList method
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            MainList.ItemsSource = LineList.Where(FilterList).ToList();
        }

        //this moves a blue dot +text over a polyline point when clicked
        private void MoveDot(double x, double y, double taskage)
        {
            if(x == 0 && y == 0 && taskage == 0)
            {
                dot.Visibility = Visibility.Hidden;
                taskageText.Visibility = Visibility.Hidden;
                return;
            }

            dot.Visibility = Visibility.Visible;
            taskageText.Visibility = Visibility.Visible;

            dot.Margin = new Thickness(x - dot.Width/2, y - dot.Height/2, 0, 0);
            taskageText.Text = taskage.ToString();
            taskageText.Margin = new Thickness(x, y, 0, 0);
        }

        //event that fires when graph is clicked: if click area is close to a point, display taskageText with proper taskage value
        private void CanGraph_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Point clickPoint = e.GetPosition(canGraph);
            Polyline graphLine;
            try
            {
                graphLine = (Polyline)canGraph.Children[2]; ;
            }
            catch
            {
                Console.WriteLine("Graph children are not in proper order, Polyline object must be in index 2");
                return;
            }
            
            foreach(Point dataPoint in graphLine.Points)
            {
                if(Math.Abs(clickPoint.X - dataPoint.X) < 5 && Math.Abs(clickPoint.Y - dataPoint.Y) < 5)
                {
                    MoveDot(dataPoint.X, dataPoint.Y, canGraph.Height - GRAPH_MARGIN - dataPoint.Y);
                    return;
                }
            }
            MoveDot(0, 0, 0);
        }

        private void ExitApp_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        //when a list item is clicked, this displays the start and end time by taking the LogLine property and stepping through LineList
        private void MainList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(e.AddedItems.Count != 1)
            {
                Console.WriteLine("Too many items selected");
                return;
            }
            LogLine selectedRow = e.AddedItems[0] as LogLine;
            string startDate = string.Empty;
            string endDate = string.Empty;
            if (string.Compare(selectedRow.Status, "Network Signon") == 0)
            {
                startDate = selectedRow.TimeStamp;
            }
            else
            {
                endDate = selectedRow.TimeStamp;
            }

            int index = LineList.IndexOf(selectedRow);
            while((index+1 < LineList.Count || string.IsNullOrEmpty(startDate)) && (index-1 >= 0 || string.IsNullOrEmpty(endDate))) //if we're counting up make sure next index exists, if counting down make sure index is not below 0
            {
                if(string.IsNullOrEmpty(startDate) == true)
                {
                    if(LineList[index-1].Task == selectedRow.Task)
                    {
                        startDate = LineList[index - 1].TimeStamp;
                        break;
                    }
                    index = index - 1;
                }
                else
                {
                    if (LineList[index+1].Task == selectedRow.Task)
                    {
                        endDate = LineList[index + 1].TimeStamp;
                        break;
                    }
                    index = index + 1;
                }
            }

            StartLabel.Text = "Start: " + startDate;
            EndLabel.Text = "End: " + endDate;
        }

        private void ChooseDate_Click(object sender, RoutedEventArgs e)
        {
            SearchServerDialog = new ServerSearchBox();
            SearchServerDialog.ConnectButton.Click += ConnectButton_Click;
            SearchServerDialog.ShowDialog();
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            SearchServerDialog.Close();
            string serverToFind = SearchServerDialog.ServerName.Text;
            string dateToSearch;
            if (SearchServerDialog.DatePick.SelectedDate == null)
            {
                //dateToSearch = @"\d{6}"; //records are WAY to big for me
                MessageBox.Show("No date provided");
                return;
            }
            else
            {
                dateToSearch = ((DateTime)SearchServerDialog.DatePick.SelectedDate).ToString("yyMMdd");
            }
            OpenLogAtDate(serverToFind, dateToSearch);
        }

        private void OpenLogAtDate(string network, string date)
        {
            bool readNextInLine = false; //this is set true after the earliest file of the next date is read; its purpose is to read the last few hours that are stored in the next day's file
            string path = @"\\qau3\Customers\" + network + @"\SUPV";
            if (Directory.Exists(path) == false)
            {
                MessageBox.Show("Can't find server " + network);
                return;
            }

            //checks constant pQS1LOG prefix, parses given date by yyMMddm then pair of 6 digit numbers
            Regex fileSatisfiesDate = new Regex(@"pQS1LOG\." + date + @"(\.\d{6}){2}\.log$");
            string superString = "";

            List<string> fileList = Directory.EnumerateFiles(path).OrderBy(x => x).ToList(); //turns enumeration into a list of string; also guarantees they are arranged in order
            for (int i = 0; i < fileList.Count; i++)
            {
                if (fileSatisfiesDate.IsMatch(fileList[i]) == true)
                {
                    superString = superString + File.ReadAllText(fileList[i]) + "NEWFILEBREAK\n";
                    readNextInLine = true;
                }
                else if (readNextInLine == true && Regex.IsMatch(fileList[i], @"\.log$") == true)
                {
                    superString = superString + File.ReadAllText(fileList[i]);
                    readNextInLine = false;
                }

            }

            MemoryStream newStream = new MemoryStream();
            StreamWriter writer = new StreamWriter(newStream);
            writer.Write(superString);
            writer.Flush();
            newStream.Position = 0;
            Tabs.IsEnabled = true;
            infile = new StreamReader(newStream);
            GatherSignOnOffs();
        }

        //returns string of all rows being selected in csv format
        //if less than 2 rows are selected, it returns everything
        private string GetSelectedRows()
        {
            string csvString = "Time,Status,Task,IP,Name\n";
            var items = MainList.SelectedItems;
            if(items.Count <= 1)
            {
                items = MainList.Items;
            }

            foreach (LogLine row in items)
            {
                csvString = csvString + row.TimeStamp + "," + row.Status + "," + row.Task + "," + row.IP + "," + row.Name + "\n";
            }
            return csvString;
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileWindow = new SaveFileDialog();
            saveFileWindow.Filter = "CSV (*.csv)|*.csv";
            if(saveFileWindow.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(saveFileWindow.FileName, GetSelectedRows());
                }
                catch(IOException exe)
                {
                    MessageBox.Show(exe.Message);
                }
            }
        }
    }
}
