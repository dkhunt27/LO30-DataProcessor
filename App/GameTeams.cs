using LO30.Data;
using LO30.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace App
{
  public class GameTeams
  {
    public void SaveOrUpdateGameTeams(Lo30Context context, Lo30ContextService lo30ContextService, string folderPath, int startingGameIdToProcess, int endingGameIdToProcess)
    {
     /* ProcessingResult pr = new ProcessingResult();
      pr.TableName = "GameTeams";
      pr.StartTime = DateTime.Now;

      dynamic parsedJson = _accessDatabaseService.ParseObjectFromJsonFile(folderPath + "Games.json");
      int count = parsedJson.Count;

      Print("SaveOrUpdateGameTeams: Access records to process:" + count);

      int countSaveOrUpdated = 0;
      for (var d = 0; d < parsedJson.Count; d++)
      {
        if (d % 100 == 0) { Print("SaveOrUpdateGameTeams: Access records processed:" + d); }
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
      /*
          homeTeamId = json["HOME_TEAM_ID"];
          awayTeamId = json["AWAY_TEAM_ID"];

          var gameTeam = new GameTeam(gid: gameId, ht: true, stid: homeTeamId);
          countSaveOrUpdated = countSaveOrUpdated + lo30ContextService.SaveOrUpdateGameTeam(gameTeam);

          gameTeam = new GameTeam(gid: gameId, ht: false, stid: awayTeamId);

          countSaveOrUpdated = countSaveOrUpdated + lo30ContextService.SaveOrUpdateGameTeam(gameTeam);
        }
      }

      Print("SaveOrUpdateGameTeams: GameTeams Count: " + context.GameTeams.Count() + " SaveOrUpdated:" + countSaveOrUpdated);
      var diffFromLast = DateTime.Now - last;
      Print("SaveOrUpdateGameTeams: TimeToProcess: " + diffFromLast.ToString());*/
    }

  }
}
