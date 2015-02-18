using App.Services;
using LO30.Data;
using LO30.Data.Objects;
using LO30.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace App.Processor
{
  public class PlayersProcessor
  {
    private OutputService _outputService;
    private AccessDatabaseService _accessDatabaseService;

    public PlayersProcessor(OutputService output, AccessDatabaseService accessDatabaseService)
    {
      _outputService = output;
      _accessDatabaseService = accessDatabaseService;
    }

    public LoggerResult SaveOrUpdatePlayers(Lo30Context context, Lo30ContextService lo30ContextService, string folderPath)
    {
      _outputService.Print("SaveOrUpdatePlayers: Starting");
      LoggerResult log = new LoggerResult("Players");

      var last = DateTime.Now;

      var player = new Player()
      {
        PlayerId = 0,
        FirstName = "Unknown",
        LastName = "Player",
        Suffix = null,
        PreferredPosition = "X",
        Shoots = "X",
        BirthDate = DateTime.Parse("1/1/1970"),
        Profession = null,
        WifesName = null
      };

      context.Players.Add(player);

      dynamic parsedJson = _accessDatabaseService.ParseObjectFromJsonFile(folderPath + "Players.json");
      int count = parsedJson.Count;
      int countSaveOrUpdated = 0;

      _outputService.Print("SaveOrUpdatePlayers: Access records to process:" + count);
      log.ProcessedCount = count;

      for (var d = 0; d < parsedJson.Count; d++)
      {
        if (d % 100 == 0) { _outputService.Print("SaveOrUpdatePlayers: Access records processed:" + d + ". Records saved or updated:" + countSaveOrUpdated); }
        var json = parsedJson[d];
        int playerId = json["PLAYER_ID"];

        string firstName = json["PLAYER_FIRST_NAME"];
        if (string.IsNullOrWhiteSpace(firstName))
        {
          firstName = "_";
        };

        string lastName = json["PLAYER_LAST_NAME"];
        if (string.IsNullOrWhiteSpace(lastName))
        {
          lastName = "_";
        };

        string position, positionMapped;
        position = json["PLAYER_POSITION"];

        if (string.IsNullOrWhiteSpace(position))
        {
          position = "X";
        }

        switch (position.ToLower())
        {
          case "f":
          case "forward":
            positionMapped = "F";
            break;
          case "d":
          case "defense":
            positionMapped = "D";
            break;
          case "g":
          case "goal":
          case "goalie":
            positionMapped = "G";
            break;
          default:
            positionMapped = "X";
            break;
        }

        string shoots, shootsMapped;
        shoots = json["SHOOTS"];
        if (string.IsNullOrWhiteSpace(shoots))
        {
          shoots = "X";
        }

        switch (shoots.ToLower())
        {
          case "l":
            shootsMapped = "L";
            break;
          case "r":
            shootsMapped = "R";
            break;
          default:
            shootsMapped = "X";
            break;
        }

        DateTime? birthDate = null;

        if (json["BIRTHDATE"] != null)
        {
          birthDate = json["BIRTHDATE"];
        }

        player = new Player()
        {
          PlayerId = playerId,
          FirstName = firstName,
          LastName = lastName,
          Suffix = json["PLAYER_SUFFIX"],
          PreferredPosition = positionMapped,
          Shoots = shootsMapped,
          BirthDate = birthDate,
          Profession = json["PROFESSION"],
          WifesName = json["WIFES_NAME"]
        };

        countSaveOrUpdated = countSaveOrUpdated + lo30ContextService.SaveOrUpdatePlayer(player);
      }

      _outputService.Print("SaveOrUpdatePlayers: Players Count:" + context.Players.Count() + " SaveOrUpdated:" + countSaveOrUpdated);
      log.End();
      _outputService.Print("SaveOrUpdatePlayers: TimeToProcess: " + log.TimeToProcess);

      return log;
    }
  }
}
