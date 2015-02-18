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
  public class GameTeamsProcessor
  {
    private OutputService _outputService;
    private AccessDatabaseService _accessDatabaseService;
    private Lo30DataService _lo30DataService;

    public GameTeamsProcessor(OutputService outputService, AccessDatabaseService accessDatabaseService, Lo30DataService lo30DataService)
    {
      _outputService = outputService;
      _accessDatabaseService = accessDatabaseService;
      _lo30DataService = lo30DataService;
    }

    public LoggerResult SaveOrUpdateGameTeams(Lo30Context context, Lo30ContextService lo30ContextService, string folderPath, int startingGameIdToProcess, int endingGameIdToProcess)
    {
      var methodName = "SaveOrUpdateGameTeams";
      var finalTarget = "GameTeams";
      _outputService.Print(methodName + ": Starting");
      LoggerResult log = new LoggerResult(finalTarget);

      dynamic parsedJson = _accessDatabaseService.ParseObjectFromJsonFile(folderPath + "Games.json");
      int count = parsedJson.Count;

      _outputService.Print(methodName + ": Access records to process:" + count);

      int countSaveOrUpdated = 0;
      for (var d = 0; d < parsedJson.Count; d++)
      {
        if (d % 100 == 0) { _outputService.Print(methodName + ": Access records processed:" + d); }
        var json = parsedJson[d];

        int gameId = json["GAME_ID"];

        if (gameId >= startingGameIdToProcess && gameId <= endingGameIdToProcess)
        {
          int homeTeamId, awayTeamId;

          /*switch (gameId)
          {
            case 3276:
              homeTeamId = 322;
              awayTeamId = 321;
              break;
            case 3277:
              homeTeamId = 324;
              awayTeamId = 323;
              break;
            case 3278:
              homeTeamId = 326;
              awayTeamId = 325;
              break;
            case 3279:
              homeTeamId = 328;
              awayTeamId = 327;
              break;
            case 3316:
              homeTeamId = 328;
              awayTeamId = 327;
              break;
            case 3317:
              homeTeamId = 326;
              awayTeamId = 325;
              break;
            case 3318:
              homeTeamId = 324;
              awayTeamId = 323;
              break;
            case 3319:
              homeTeamId = 322;
              awayTeamId = 321;
              break;
            default:
              homeTeamId = json["HOME_TEAM_ID"];
              awayTeamId = json["AWAY_TEAM_ID"];
              break;
          };*/

          homeTeamId = json["HOME_TEAM_ID"];
          awayTeamId = json["AWAY_TEAM_ID"];

          var gameTeam = new GameTeam(gid: gameId, ht: true, stid: homeTeamId);
          countSaveOrUpdated = countSaveOrUpdated + lo30ContextService.SaveOrUpdateGameTeam(gameTeam);

          gameTeam = new GameTeam(gid: gameId, ht: false, stid: awayTeamId);

          countSaveOrUpdated = countSaveOrUpdated + lo30ContextService.SaveOrUpdateGameTeam(gameTeam);
        }
      }

      _outputService.Print(methodName + ": GameTeams Count:" + context.GameTeams.Count() + " SaveOrUpdated:" + countSaveOrUpdated);
      log.End();
      _outputService.Print(methodName + ": TimeToProcess: " + log.TimeToProcess);

      return log;
    }

  }
}
