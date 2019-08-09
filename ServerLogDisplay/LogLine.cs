using System;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace ServerLogDisplay
{

    public class LogLine
    {
        public string TimeStamp { get; set; }
        public string Status { get; set; }
        public string Task { get; set; }
        public string IP { get; set; }
        public string Name { get; set; }
        public Brush Color { get; set; }

        private string HexStrToDecStr(string hexStr)
        {
            int hexToDec = Convert.ToInt32(hexStr, 16);
            if(hexToDec >= 32768) //number is compatible with 2's comp
            {
                hexToDec = hexToDec - 65535 - 1; //hex - FFFF - 1
            }
            
            return hexToDec.ToString();
        }

        public LogLine(string line, int status)
        {
            //set default strings to ""
            TimeStamp = "";
            Status = "";
            Task = "";
            IP = "";
            Name = "";

            Match timeMatch = Regex.Match(line, @"^\d{6}\.\d{6}\.\d{3}"); //takes the date; parses by yymmdd.hhmmss.nnn
            Match taskMatch = Regex.Match(line, @"\| T\w\w\w\|"); //takes the task no. in hex; parses by |T XXX|
            Match ipMatch = Regex.Match(line, @"\| \w{8} \w{8} \w{8}"); //takes the next three doublewords
            Match nameMatch = Regex.Match(line, ".{16}$"); //takes the secondary name given by client; parses by the last 16 chars on the line

            if (timeMatch.Success == true)
            {
                TimeStamp = timeMatch.ToString().Substring(2, 2) + "/" + timeMatch.ToString().Substring(4, 2) + "/" + timeMatch.ToString().Substring(0, 2)
                    + " - " + timeMatch.ToString().Substring(7, 2) + ":" + timeMatch.ToString().Substring(9, 2) + ":" + timeMatch.ToString().Substring(11, 2);
            }
            if(status == 1)
            {
                Status = "Network Signon";
                Color = Brushes.Navy;
            }
            else if(status == 2)
            {
                Status = "Network Close";
                Color = Brushes.Crimson;
            }
            else
            {
                Status = "Disconnect Error";
                Color = Brushes.Crimson;
            }
            if(taskMatch.Success == true)
            {
                Task = HexStrToDecStr(taskMatch.ToString().Substring(3, 3));
            }
            if(status == 1 && ipMatch.Success == true)
            {
                string ip = ipMatch.ToString().Substring(11, 8);

                IP = HexStrToDecStr(ip.Substring(0, 2)) + "." + HexStrToDecStr(ip.Substring(2, 2)) + "." + HexStrToDecStr(ip.Substring(4, 2)) + "." + HexStrToDecStr(ip.Substring(6, 2));
            }
            else if(status == 0 && ipMatch.Success == true)
            {
                string errorCode = ipMatch.ToString().Substring(15, 4);
                Status = Status + ": " + HexStrToDecStr(errorCode);
            }
            if(nameMatch.Success == true)
            {
                Name = nameMatch.ToString();
            }
        }
    }
}
