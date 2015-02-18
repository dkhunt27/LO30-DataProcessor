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
  public class GameRostersProcessor
  {
    private OutputService _outputService;
    private AccessDatabaseService _accessDatabaseService;
    private Lo30DataService _lo30DataService;

    public GameRostersProcessor(OutputService outputService, AccessDatabaseService accessDatabaseService, Lo30DataService lo30DataService)
    {
      _outputService = outputService;
      _accessDatabaseService = accessDatabaseService;
      _lo30DataService = lo30DataService;
    }

    public LoggerResult SaveOrUpdateGameRosters(Lo30Context context, Lo30ContextService lo30ContextService, string folderPath, int startingGameIdToProcess, int endingGameIdToProcess)
    {
      var methodName = "SaveOrUpdateGameRosters";
      _outputService.Print(methodName + ": Starting");
      LoggerResult log = new LoggerResult("Players");

      dynamic parsedJsonGR = _accessDatabaseService.ParseObjectFromJsonFile(folderPath + "GameRosters.json");
      int countGR = parsedJsonGR.Count;
      int countSaveOrUpdated = 0;

      _outputService.Print(methodName + ": Access records to process:" + countGR);
      log.ProcessedCount = countGR;

      for (var d = 0; d < parsedJsonGR.Count; d++)
      {
        if (d % 100 == 0) { _outputService.Print(methodName + ": Access records processed:" + d + ". Records saved or updated:" + countSaveOrUpdated); }
        var json = parsedJsonGR[d];

        int seasonId = json["SEASON_ID"];
        int gameId = json["GAME_ID"];

        if (gameId >= startingGameIdToProcess && gameId <= endingGameIdToProcess)
        {
          var game = lo30ContextService.FindGame(gameId);
          var gameDateYYYYMMDD = _lo30DataService.ConvertDateTimeIntoYYYYMMDD(game.GameDateTime, ifNullReturnMax: false);

          var homeGameTeamId = lo30ContextService.FindGameTeamByPK2(gameId, homeTeam: true).GameTeamId;
          var awayGameTeamId = lo30ContextService.FindGameTeamByPK2(gameId, homeTeam: false).GameTeamId;

          int homeTeamId = -1;
          if (json["HOME_TEAM_ID"] != null)
          {
            homeTeamId = json["HOME_TEAM_ID"];
          }

          int homePlayerId = -1;
          if (json["HOME_PLAYER_ID"] != null)
          {
            homePlayerId = json["HOME_PLAYER_ID"];
          }

          int homeSubPlayerId = -1;
          if (json["HOME_SUB_FOR_PLAYER_ID"] != null)
          {
            homeSubPlayerId = json["HOME_SUB_FOR_PLAYER_ID"];
          }

          bool homePlayerSubInd = false;
          if (json["HOME_PLAYER_SUB_IND"] != null)
          {
            homePlayerSubInd = json["HOME_PLAYER_SUB_IND"];
          }

          int homePlayerNumber = -1;
          if (json["HOME_PLAYER_NUMBER"] != null)
          {
            homePlayerNumber = json["HOME_PLAYER_NUMBER"];
          }

          if (homeTeamId == -1)
          {
            _outputService.Print(string.Format("SaveOrUpdateGameRosters: The homeTeamId is -1, not sure how to process. homeTeamId:{0}, homePlayerId:{1}, homeSubPlayerId:{2}, homePlayerSubInd:{3}, homePlayerNumber:{4}, gameId:{5}", homeTeamId, homePlayerId, homeSubPlayerId, homePlayerSubInd, homePlayerNumber, gameId));
          }
          else if (homePlayerId == -1)
          {
            _outputService.Print(string.Format("SaveOrUpdateGameRosters: The homePlayerId is -1, not sure how to process. homeTeamId:{0}, homePlayerId:{1}, homeSubPlayerId:{2}, homePlayerSubInd:{3}, homePlayerNumber:{4}, gameId:{5}", homeTeamId, homePlayerId, homeSubPlayerId, homePlayerSubInd, homePlayerNumber, gameId));
          }
          else if (homePlayerNumber == -1)
          {
            _outputService.Print(string.Format("SaveOrUpdateGameRosters: The homePlayerId is -1, not sure how to process. homeTeamId:{0}, homePlayerId:{1}, homeSubPlayerId:{2}, homePlayerSubInd:{3}, homePlayerNumber:{4}, gameId:{5}", homeTeamId, homePlayerId, homeSubPlayerId, homePlayerSubInd, homePlayerNumber, gameId));
          }

          // set the line and position equal to the players drafted / set line position from the team roster
          var homeTeamRoster = lo30ContextService.FindTeamRosterWithYYYYMMDD(homeTeamId, homePlayerId, gameDateYYYYMMDD);
          int homePlayerLine = homeTeamRoster.Line;
          string homePlayerPosition = homeTeamRoster.Position;

          int playerId;
          int? subbingForPlayerId;

          if (homePlayerSubInd)
          {
            playerId = homeSubPlayerId;
            subbingForPlayerId = homePlayerId;
          }
          else
          {
            playerId = homePlayerId;
            subbingForPlayerId = null;
          }

          bool isGoalie = false;
          if (homeTeamRoster.Position == "G")
          {
            isGoalie = true;
          }

          int ratingPrimary = 0;
          int ratingSecondary = 0;
          var playerRating = lo30ContextService.FindPlayerRatingWithYYYYMMDD(playerId, homePlayerPosition, seasonId, gameDateYYYYMMDD, errorIfNotFound: false);

          if (playerRating != null)
          {
            ratingPrimary = playerRating.RatingPrimary;
            ratingSecondary = playerRating.RatingSecondary;
          }
          var gameRoster = new GameRoster(
                                  gtid: homeGameTeamId,
                                  pn: homePlayerNumber.ToString(),
                                  line: homePlayerLine,
                                  pos: homePlayerPosition,
                                  g: isGoalie,
                                  pid: playerId,
                                  rp: ratingPrimary,
                                  rs: ratingSecondary,
                                  sub: homePlayerSubInd,
                                  sfpid: subbingForPlayerId
                            );

          countSaveOrUpdated = countSaveOrUpdated + lo30ContextService.SaveOrUpdateGameRoster(gameRoster);

          int awayTeamId = -1;
          if (json["AWAY_TEAM_ID"] != null)
          {
            awayTeamId = json["AWAY_TEAM_ID"];
          }

          int awayPlayerId = -1;
          if (json["AWAY_PLAYER_ID"] != null)
          {
            awayPlayerId = json["AWAY_PLAYER_ID"];
          }

          int awaySubPlayerId = -1;
          if (json["AWAY_SUB_FOR_PLAYER_ID"] != null)
          {
            awaySubPlayerId = json["AWAY_SUB_FOR_PLAYER_ID"];
          }

          bool awayPlayerSubInd = false;
          if (json["AWAY_PLAYER_SUB_IND"] != null)
          {
            awayPlayerSubInd = json["AWAY_PLAYER_SUB_IND"];

          }

          int awayPlayerNumber = -1;
          if (json["AWAY_PLAYER_NUMBER"] != null)
          {
            awayPlayerNumber = json["AWAY_PLAYER_NUMBER"];
          }

          if (awayTeamId == -1)
          {
            _outputService.Print(string.Format("SaveOrUpdateGameRosters: The awayTeamId is -1, not sure how to process. awayTeamId:{0}, awayPlayerId:{1}, awaySubPlayerId:{2}, awayPlayerSubInd:{3}, awayPlayerNumber:{4}, gameId:{5}", awayTeamId, awayPlayerId, awaySubPlayerId, awayPlayerSubInd, awayPlayerNumber, gameId));
          }
          else if (awayPlayerId == -1)
          {
            _outputService.Print(string.Format("SaveOrUpdateGameRosters: The awayPlayerId is -1, not sure how to process. awayTeamId:{0}, awayPlayerId:{1}, awaySubPlayerId:{2}, awayPlayerSubInd:{3}, awayPlayerNumber:{4}, gameId:{5}", awayTeamId, awayPlayerId, awaySubPlayerId, awayPlayerSubInd, awayPlayerNumber, gameId));
          }
          else if (awayPlayerNumber == -1)
          {
            _outputService.Print(string.Format("SaveOrUpdateGameRosters: The awayPlayerId is -1, not sure how to process. awayTeamId:{0}, awayPlayerId:{1}, awaySubPlayerId:{2}, awayPlayerSubInd:{3}, awayPlayerNumber:{4}, gameId:{5}", awayTeamId, awayPlayerId, awaySubPlayerId, awayPlayerSubInd, awayPlayerNumber, gameId));
          }

          if (awayPlayerSubInd)
          {
            playerId = awaySubPlayerId;
            subbingForPlayerId = awayPlayerId;
          }
          else
          {
            playerId = awayPlayerId;
            subbingForPlayerId = null;
          }

          // set the line and position equal to the players drafted / set line position from the team roster
          var awayTeamRoster = lo30ContextService.FindTeamRosterWithYYYYMMDD(awayTeamId, awayPlayerId, gameDateYYYYMMDD);
          int awayPlayerLine = awayTeamRoster.Line;
          string awayPlayerPosition = awayTeamRoster.Position;

          isGoalie = false;
          if (awayTeamRoster.Position == "G")
          {
            isGoalie = true;
          }

          ratingPrimary = 0;
          ratingSecondary = 0;
          playerRating = lo30ContextService.FindPlayerRatingWithYYYYMMDD(playerId, awayPlayerPosition, seasonId, gameDateYYYYMMDD, errorIfNotFound: false);

          if (playerRating != null)
          {
            ratingPrimary = playerRating.RatingPrimary;
            ratingSecondary = playerRating.RatingSecondary;
          }

          gameRoster = new GameRoster(
                                  gtid: awayGameTeamId,
                                  pn: awayPlayerNumber.ToString(),
                                  line: awayPlayerLine,
                                  pos: awayPlayerPosition,
                                  g: isGoalie,
                                  pid: playerId,
                                  rp: ratingPrimary,
                                  rs: ratingSecondary,
                                  sub: awayPlayerSubInd,
                                  sfpid: subbingForPlayerId
                            );

          countSaveOrUpdated = countSaveOrUpdated + lo30ContextService.SaveOrUpdateGameRoster(gameRoster);
        }
      }

      _outputService.Print(methodName + ": GameRosters Count:" + context.GameRosters.Count() + " SaveOrUpdated:" + countSaveOrUpdated);
      log.End();
      _outputService.Print(methodName + ": TimeToProcess: " + log.TimeToProcess);

      return log;
    }

    public LoggerResult processScoreSheetEntrySubsIntoGameRosters(Lo30Context context, Lo30ContextService lo30ContextService, string folderPath, int startingGameIdToProcess, int endingGameIdToProcess)
    {
      var methodName = "processScoreSheetEntrySubsIntoGameRosters";
      _outputService.Print(methodName + ": Starting");
      LoggerResult log = new LoggerResult("GameRosters");
      /*
      dynamic parsedJson = _accessDatabaseService.ParseObjectFromJsonFile(folderPath + "ScoreSheetEntrySubs.json");
      int countSSES = parsedJson.Count;
      int countSaveOrUpdated = 0;

      _outputService.Print(methodName + ": Access records to process:" + countSSES);
      log.ProcessedCount = countSSES;

      for (var d = 0; d < parsedJson.Count; d++)
      {
        if (d % 100 == 0) { _outputService.Print(methodName + ": Processed:" + d + ". Saved/Updated:" + countSaveOrUpdated); }
        var json = parsedJson[d];

        int seasonId = json["SEASON_ID"];
        int gameId = json["GAME_ID"];
        int team = json["TEAM"];
        int jersey = json["JERSEY"];
        int subId = json["SUB_ID"];
        int subForId = json["SUB_FOR_ID"];
        DateTime updatedOn = json["UPDATED_ON"];

        if (gameId >= startingGameIdToProcess && gameId <= endingGameIdToProcess)
        {
          var game = lo30ContextService.FindGame(gameId);
          var gameDateYYYYMMDD = _lo30DataService.ConvertDateTimeIntoYYYYMMDD(game.GameDateTime, ifNullReturnMax: false);

          var homeGameTeam = lo30ContextService.FindGameTeamByPK2(gameId, homeTeam: true);
          var awayGameTeam = lo30ContextService.FindGameTeamByPK2(gameId, homeTeam: false);

          var homeTeamRoster = lo30ContextService.FindTeamRostersWithYYYYMMDD(homeGameTeam.SeasonTeamId, gameDateYYYYMMDD);
          var awayTeamRoster = lo30ContextService.FindTeamRostersWithYYYYMMDD(awayGameTeam.SeasonTeamId, gameDateYYYYMMDD);

          foreach(var htRoster in homeTeamRoster)
          {
            // set the line and position equal to the players drafted / set line position from the team roster
            int homePlayerLine = htRoster.Line;
            string homePlayerPosition = htRoster.Position;

            var gameRosterNoSub = processIntoGameRoster(lo30ContextService, htRoster.PlayerId, htRoster.Position, seasonId, gameDateYYYYMMDD, homeGameTeam.GameTeamId, htRoster.PlayerNumber, htRoster.Line);

            int playerId;
            int? subbingForPlayerId;

            if (homePlayerSubInd)
            {
              playerId = homeSubPlayerId;
              subbingForPlayerId = homePlayerId;
            }
            else
            {
              playerId = homePlayerId;
              subbingForPlayerId = null;
            }

            bool isGoalie = false;
            if (homeTeamRoster.Position == "G")
            {
              isGoalie = true;
            }

            int ratingPrimary = 0;
            int ratingSecondary = 0;
            var playerRating = lo30ContextService.FindPlayerRatingWithYYYYMMDD(playerId, homePlayerPosition, seasonId, gameDateYYYYMMDD, errorIfNotFound: false);

            if (playerRating != null)
            {
              ratingPrimary = playerRating.RatingPrimary;
              ratingSecondary = playerRating.RatingSecondary;
            }
            var gameRoster = new GameRoster(
                                    gtid: homeGameTeamId,
                                    pn: homePlayerNumber.ToString(),
                                    line: homePlayerLine,
                                    pos: homePlayerPosition,
                                    g: isGoalie,
                                    pid: playerId,
                                    rp: ratingPrimary,
                                    rs: ratingSecondary,
                                    sub: homePlayerSubInd,
                                    sfpid: subbingForPlayerId
                              );

            countSaveOrUpdated = countSaveOrUpdated + lo30ContextService.SaveOrUpdateGameRoster(gameRoster);
          }



          int awayTeamId = -1;
          if (json["AWAY_TEAM_ID"] != null)
          {
            awayTeamId = json["AWAY_TEAM_ID"];
          }

          int awayPlayerId = -1;
          if (json["AWAY_PLAYER_ID"] != null)
          {
            awayPlayerId = json["AWAY_PLAYER_ID"];
          }

          int awaySubPlayerId = -1;
          if (json["AWAY_SUB_FOR_PLAYER_ID"] != null)
          {
            awaySubPlayerId = json["AWAY_SUB_FOR_PLAYER_ID"];
          }

          bool awayPlayerSubInd = false;
          if (json["AWAY_PLAYER_SUB_IND"] != null)
          {
            awayPlayerSubInd = json["AWAY_PLAYER_SUB_IND"];

          }

          int awayPlayerNumber = -1;
          if (json["AWAY_PLAYER_NUMBER"] != null)
          {
            awayPlayerNumber = json["AWAY_PLAYER_NUMBER"];
          }

          if (awayTeamId == -1)
          {
            _outputService.Print(string.Format("SaveOrUpdateGameRosters: The awayTeamId is -1, not sure how to process. awayTeamId:{0}, awayPlayerId:{1}, awaySubPlayerId:{2}, awayPlayerSubInd:{3}, awayPlayerNumber:{4}, gameId:{5}", awayTeamId, awayPlayerId, awaySubPlayerId, awayPlayerSubInd, awayPlayerNumber, gameId));
          }
          else if (awayPlayerId == -1)
          {
            _outputService.Print(string.Format("SaveOrUpdateGameRosters: The awayPlayerId is -1, not sure how to process. awayTeamId:{0}, awayPlayerId:{1}, awaySubPlayerId:{2}, awayPlayerSubInd:{3}, awayPlayerNumber:{4}, gameId:{5}", awayTeamId, awayPlayerId, awaySubPlayerId, awayPlayerSubInd, awayPlayerNumber, gameId));
          }
          else if (awayPlayerNumber == -1)
          {
            _outputService.Print(string.Format("SaveOrUpdateGameRosters: The awayPlayerId is -1, not sure how to process. awayTeamId:{0}, awayPlayerId:{1}, awaySubPlayerId:{2}, awayPlayerSubInd:{3}, awayPlayerNumber:{4}, gameId:{5}", awayTeamId, awayPlayerId, awaySubPlayerId, awayPlayerSubInd, awayPlayerNumber, gameId));
          }

          if (awayPlayerSubInd)
          {
            playerId = awaySubPlayerId;
            subbingForPlayerId = awayPlayerId;
          }
          else
          {
            playerId = awayPlayerId;
            subbingForPlayerId = null;
          }

          // set the line and position equal to the players drafted / set line position from the team roster
          var awayTeamRoster = lo30ContextService.FindTeamRosterWithYYYYMMDD(awayTeamId, awayPlayerId, gameDateYYYYMMDD);
          int awayPlayerLine = awayTeamRoster.Line;
          string awayPlayerPosition = awayTeamRoster.Position;

          isGoalie = false;
          if (awayTeamRoster.Position == "G")
          {
            isGoalie = true;
          }

          ratingPrimary = 0;
          ratingSecondary = 0;
          playerRating = lo30ContextService.FindPlayerRatingWithYYYYMMDD(playerId, awayPlayerPosition, seasonId, gameDateYYYYMMDD, errorIfNotFound: false);

          if (playerRating != null)
          {
            ratingPrimary = playerRating.RatingPrimary;
            ratingSecondary = playerRating.RatingSecondary;
          }

          gameRoster = new GameRoster(
                                  gtid: awayGameTeamId,
                                  pn: awayPlayerNumber.ToString(),
                                  line: awayPlayerLine,
                                  pos: awayPlayerPosition,
                                  g: isGoalie,
                                  pid: playerId,
                                  rp: ratingPrimary,
                                  rs: ratingSecondary,
                                  sub: awayPlayerSubInd,
                                  sfpid: subbingForPlayerId
                            );

          countSaveOrUpdated = countSaveOrUpdated + lo30ContextService.SaveOrUpdateGameRoster(gameRoster);
        }
      }

      _outputService.Print("SaveOrUpdatePlayers: Players Count:" + context.Players.Count() + " SaveOrUpdated:" + countSaveOrUpdated);
      log.End();
      _outputService.Print("SaveOrUpdatePlayers: TimeToProcess: " + log.TimeToProcess);
      */
      return log;
    }

    private GameRoster processIntoGameRoster(Lo30ContextService lo30ContextService, int playerId, string position, int seasonId, int gameDateYYYYMMDD, int gameTeamId, string jerseyNumber, int line)
    {
      int? subbingForPlayerId = null;

      bool isGoalie = false;
      if (position == "G")
      {
        isGoalie = true;
      }

      int ratingPrimary = 0;
      int ratingSecondary = 0;
      var playerRating = lo30ContextService.FindPlayerRatingWithYYYYMMDD(playerId, position, seasonId, gameDateYYYYMMDD, errorIfNotFound: false);

      if (playerRating != null)
      {
        ratingPrimary = playerRating.RatingPrimary;
        ratingSecondary = playerRating.RatingSecondary;
      }
      var gameRoster = new GameRoster(
                              gtid: gameTeamId,
                              pn: jerseyNumber,
                              line: line,
                              pos: position,
                              g: isGoalie,
                              pid: playerId,
                              rp: ratingPrimary,
                              rs: ratingSecondary,
                              sub: false,
                              sfpid: subbingForPlayerId
                        );

      return gameRoster;
    }
  }
}
