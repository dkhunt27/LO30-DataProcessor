using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace App.Services
{
  public class OutputService
  {
    private StreamWriter _logFile;

    public OutputService(StreamWriter logFile)
    {
      _logFile = logFile;
    }

    public void Print(string message)
    {
      Debug.Print(message);
      Console.WriteLine(message);
      _logFile.WriteLine(message);
    }

  }
}
