using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;

namespace ServerLogDisplay
{
    class UsageGraph
    {
        private double xMin;
        private double xMax;
        private double yMin;
        private double yMax;
        public double margin { get; set; }
        public List<UIElement> labelList { get; }
        public double initialWidth { get; set; }
        private GeometryGroup xGeo;
        private GeometryGroup yGeo;
        private PointCollection points;


        public UsageGraph(double xmin, double xmax, double ymin, double ymax, double xactual, double yactual, double graphmargin)
        {
            margin = graphmargin;
            xMin = xmin;
            xMax = xmax;
            yMin = ymin;
            yMax = ymax;
            initialWidth = -1;
            points = new PointCollection();

            //instantiate x-axis
            xGeo = new GeometryGroup();
            xGeo.Children.Add(new LineGeometry(new Point(0, yMax), new Point(43800, yMax)));

            //instantiate y-axis
            yGeo = new GeometryGroup();
            yGeo.Children.Add(new LineGeometry(new Point(xMin, 0), new Point(xMin, yactual)));

            //instantiate label list with constant y-labels
            labelList = new List<UIElement>();
            for(int i = 0; i < 256; i+=50)
            {
                CreateNewLabelMark(i.ToString(), margin - 25, i+30);
            }
        }

        public Path getXaxis()
        {
            Path path = new Path();
            path.StrokeThickness = 1;
            path.Stroke = Brushes.Black;
            path.Data = xGeo;
            return path;
        }

        public Path getYaxis()
        {
            Path path = new Path();
            path.StrokeThickness = 1;
            path.Stroke = Brushes.Black;
            path.Data = yGeo;
            return path;
        }

        public Polyline getLine()
        {
            Polyline newLine = new Polyline();
            newLine.StrokeThickness = 1.5;
            newLine.Stroke = Brushes.Navy;
            newLine.Points = points;
            return newLine;
        }

        private void CreateNewLabelMark(string labelContent, double fromLeft, double fromBottom)
        {
            if(initialWidth < 0 && string.Compare(labelContent, "0:00") == 0)
            {
                initialWidth = fromLeft;
                //Line divider = new Line();
                //divider.StrokeThickness = 1;
                //divider.Stroke = Brushes.Gray;
                //divider.X1 = fromLeft;
                //divider.Y1 = yMin;
                //divider.X2 = fromLeft;
                //divider.Y2 = yMax+margin;
                //labelList.Add(divider);
            }

            TextBlock newLabelMark = new TextBlock();
            newLabelMark.Text = labelContent;
            newLabelMark.Foreground = Brushes.Black;
            Canvas.SetLeft(newLabelMark, fromLeft);
            Canvas.SetBottom(newLabelMark, fromBottom);
            labelList.Add(newLabelMark);
        }

        private string ConvertDateTimeToString(DateTime time)
        {
            string year = time.Year.ToString().Substring(2, 2);
            string month = time.Month.ToString();
            if(time.Month < 10)
            {
                month = "0" + month;
            }
            string day = time.Day.ToString();
            if(time.Day < 10)
            {
                day = "0" + day;
            }
            string hour = time.Hour.ToString();
            if(time.Hour < 10)
            {
                hour = "0" + hour;
            }
            string minute = time.Minute.ToString();
            if(time.Minute < 10)
            {
                minute = "0" + minute;
            }

            return year + month + day + "." + hour + minute;
        }



        public double CreateGraph(Dictionary<string, int> taskagePerDate)
        {
            //MAKE SURE that every key string is in the right format yymmdd.hhmm
            string iniStr = taskagePerDate.Keys.First();
            string finStr = taskagePerDate.Keys.Last();
            DateTime startTime = new DateTime(Int32.Parse("20"+iniStr.Substring(0, 2)), Int32.Parse(iniStr.Substring(2, 2)), Int32.Parse(iniStr.Substring(4, 2)), Int32.Parse(iniStr.Substring(7, 2)), Int32.Parse(iniStr.Substring(9, 2)), 0);
            DateTime endTime = new DateTime(Int32.Parse("20" + finStr.Substring(0, 2)), Int32.Parse(finStr.Substring(2, 2)), Int32.Parse(finStr.Substring(4, 2)), Int32.Parse(finStr.Substring(7, 2)), Int32.Parse(finStr.Substring(9, 2)), 0);
            double totalMinElapsed = endTime.Subtract(startTime).TotalMinutes;

            int i = 0;
            double lastUsedValue = 0;
            while(i <= totalMinElapsed)
            {
                string key = ConvertDateTimeToString(startTime.AddMinutes(i));
                double value = lastUsedValue;
                if(taskagePerDate.ContainsKey(key) == true)
                {
                    value = yMax - taskagePerDate[key];
                }
                points.Add(new Point(i + xMin, value));


                if((startTime.Minute + i)%60 == 0) //basically, this checks if we're at a new hour, then creates a textblock to label that hour as a tick mark
                {
                    CreateNewLabelMark(startTime.AddMinutes(i).Hour.ToString() + ":00", i + xMin, margin - 20);
                }

                
                i = i + 1;
                lastUsedValue = value;
            }

            if(totalMinElapsed < 718)
            {
                return 718;
            }
            return totalMinElapsed;
        }
    }
}
