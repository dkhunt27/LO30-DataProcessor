using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace App.Services
{
  public class Logger
  {
    public List<LoggerResult> Results { get; set; }

    public Logger()
    {
      Results = new List<LoggerResult>();
    }
  }

  public class LoggerResult
  {    
    public string TableName { get; set; }
    public int ProcessedCount { get; set; }
    public int SavedOrUpdatedCount { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public string TimeToProcess
    {
      get
      {
        return (EndTime - StartTime).ToString();
      }
    }

    public LoggerResult(string tableName)
    {
      TableName = tableName;
      StartTime = DateTime.Now;
    }

    public void End()
    {
      EndTime = DateTime.Now;
    }


  }
}
