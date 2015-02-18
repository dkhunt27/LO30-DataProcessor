using App.Processor;
using App.Services;
using LO30.Data;
using LO30.Data.Objects;
using LO30.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace App
{
  class Program
  {
    private static string _lo30ReportingDBConnString;
    private static AccessDatabaseService _accessDatabaseService;
    private static Lo30DataService _lo30DataService;
    private static OutputService _outputService;
    private static Logger _logger;
    private static StreamWriter _logFile;
    private static PlayersProcessor _playersProcessor;
    private static GameRostersProcessor _gameRostersProcessor;
    private static GameTeamsProcessor _gameTeamsProcessor;

    static void Main(string[] args)
    {
      _logFile = File.CreateText(@"C:\git\LO30\log.txt");

      try
      {
        _lo30ReportingDBConnString = System.Configuration.ConfigurationManager.ConnectionStrings["LO30ReportingDB"].ConnectionString;
        _accessDatabaseService = new AccessDatabaseService();
        _lo30DataService = new Lo30DataService();
        _outputService = new OutputService(_logFile);
        _logger = new Logger();

        _playersProcessor = new PlayersProcessor(_outputService, _accessDatabaseService);
        _gameRostersProcessor = new GameRostersProcessor(_outputService, _accessDatabaseService, _lo30DataService);
        _gameTeamsProcessor = new GameTeamsProcessor(_outputService, _accessDatabaseService, _lo30DataService);

        // SAVE ACCESS DB TO FLAT FILES
        Console.WriteLine("Saving Access DB to JSON Files");
        _accessDatabaseService.SaveTablesToJson();
        Console.WriteLine("Saved Access DB to JSON Files");

        // seed if empty database, otherwise just load new data
        bool seed = false;

        // THEN PROCESS SCORE SHEETS
        int seasonId = 54;
        bool playoff = true;
        int startingGameId = 3324;
        int endingGameId = 3346;

        //bool playoff = false;
        //int startingGameId = 3200;
        //int endingGameId = 3319;

        if (seed)
        {
          Console.WriteLine("Seeding DB");
          Seed();
          Console.WriteLine("Seeded DB");
        }
        else
        {
          Console.WriteLine("Loading New Data");
          LoadNewData(seasonId, playoff, startingGameId, endingGameId);
          Console.WriteLine("Loaded New Data");
        }

        Console.WriteLine("Processing Score Sheets");
        ProcessScoreSheets(seasonId, playoff, startingGameId, endingGameId);
        Console.WriteLine("Processed Score Sheets");

        // UPDATE Current Status
        UpdateCurrentStatus();
      }
      catch (Exception ex)
      {
        Print(ex.Message);
        var innerEx = ex.InnerException;

        while (innerEx != null)
        {
          Print("With inner exception of:");
          Print(innerEx.Message);

          innerEx = innerEx.InnerException;
        }
      }
      finally
      {
        _logFile.Close();
      }
    }

    private static void UpdateCurrentStatus()
    {
      DateTime first = DateTime.Now;
      DateTime last = DateTime.Now;
      TimeSpan diffFromLast = new TimeSpan();

      using (var context = new Lo30Context())
      {
        var _lo30ContextService = new Lo30ContextService(context);

        // determine the current status
        var currentPlayerStatus = context.PlayerStatuses
                                            .GroupBy(x => new { x.PlayerId })
                                            .Select(grp => new
                                            {
                                              PlayerId = grp.Key.PlayerId,
                                              EndYYYYMMDD = grp.Max(x => x.EndYYYYMMDD)
                                            })
                                            .ToList();

        // now update those records
        foreach (var current in currentPlayerStatus)
        {
          var playerStatus = context.PlayerStatuses.Where(x => x.PlayerId == current.PlayerId && x.EndYYYYMMDD == current.EndYYYYMMDD).FirstOrDefault();
          playerStatus.CurrentStatus = true;
        }

        int updated = _lo30ContextService.ContextSaveChanges();
        Print("Data Group 3: Updated PlayerStatuses Current " + updated);
        diffFromLast = DateTime.Now - last;
        Print("TimeToProcess: " + diffFromLast.ToString());
      }
    }

    private static void Print(string message)
    {
      Debug.Print(message);
      Console.WriteLine(message);
      _logFile.WriteLine(message);
    }

    private static void LoadNewData(int seasonIdToProcess, bool playoffToProcess, int startingGameIdToProcess, int endingGameIdToProcess)
    {
      string appDataPath = @"C:\git\LO30\LO30\App_Data\";
      var folderPath = Path.Combine(appDataPath, "Access");
      folderPath = folderPath + @"\";

      DateTime first = DateTime.Now;
      DateTime last = DateTime.Now;
      TimeSpan diffFromFirst = new TimeSpan();
      TimeSpan diffFromLast = new TimeSpan();

      using (var context = new Lo30Context())
      {
        var lo30ContextService = new Lo30ContextService(context);

        //_logger.Results.Add(_playersProcessor.SaveOrUpdatePlayers(context, _lo30ContextService, folderPath, _accessDatabaseService));
        //SaveOrUpdateSeasons(context, lo30ContextService, folderPath, seasonIdToProcess);
        //SaveOrUpdateTeamRosters(context, lo30ContextService, folderPath, seasonIdToProcess, playoffToProcess);
        SaveOrUpdateGames(context, lo30ContextService, folderPath, startingGameIdToProcess, endingGameIdToProcess);
        _logger.Results.Add(_gameTeamsProcessor.SaveOrUpdateGameTeams(context, lo30ContextService, folderPath, startingGameIdToProcess, endingGameIdToProcess));
        if (playoffToProcess)
        {
          //_logger.Results.Add(_gameRostersProcessor.SaveOrUpdateGameRosters(context, lo30ContextService, folderPath, startingGameIdToProcess, endingGameIdToProcess));
          // need to process scoresheetentrysubs after it has been loaded
        }
        else
        {
          _logger.Results.Add(_gameRostersProcessor.SaveOrUpdateGameRosters(context, lo30ContextService, folderPath, startingGameIdToProcess, endingGameIdToProcess));
        }

        #region 4:ScoreSheetEntries...using loadJson
        List<ScoreSheetEntry> scoreSheetEntries = ScoreSheetEntry.LoadListFromAccessDbJsonFile(folderPath + "ScoreSheetEntries.json", startingGameIdToProcess, endingGameIdToProcess);
        int countSaveOrUpdated = lo30ContextService.SaveOrUpdateScoreSheetEntry(scoreSheetEntries);
        Print("SaveOrUpdateScoreSheetEntries: ScoreSheetEntries Count:" + context.ScoreSheetEntries.Count() + " SaveOrUpdated:" + countSaveOrUpdated);
        diffFromLast = DateTime.Now - last;
        Print("TimeToProcess: " + diffFromLast.ToString());
        #endregion

        #region 4:ScoreSheetEntryPenalties...using loadJson
        List<ScoreSheetEntryPenalty> scoreSheetEntryPenalties = ScoreSheetEntryPenalty.LoadListFromAccessDbJsonFile(folderPath + "ScoreSheetEntryPenalties.json", startingGameIdToProcess, endingGameIdToProcess);
        countSaveOrUpdated = lo30ContextService.SaveOrUpdateScoreSheetEntryPenalty(scoreSheetEntryPenalties);
        Print("SaveOrUpdateScoreSheetEntryPenalties: ScoreSheetEntryPenalties Count:" + context.ScoreSheetEntryPenalties.Count() + " SaveOrUpdated:" + countSaveOrUpdated);
        diffFromLast = DateTime.Now - last;
        Print("TimeToProcess: " + diffFromLast.ToString());
        #endregion

        #region 4:ScoreSheetEntrySubs...using loadJson
        List<ScoreSheetEntrySub> scoreSheetEntrySubs = ScoreSheetEntrySub.LoadListFromAccessDbJsonFile(folderPath + "ScoreSheetEntrySubs.json", startingGameIdToProcess, endingGameIdToProcess);
        countSaveOrUpdated = lo30ContextService.SaveOrUpdateScoreSheetEntrySub(scoreSheetEntrySubs);
        Print("Data Group 4: ScoreSheetEntrySubs Count:" + context.ScoreSheetEntrySubs.Count() + " SaveOrUpdated:" + countSaveOrUpdated);
        diffFromLast = DateTime.Now - last;
        Print("TimeToProcess: " + diffFromLast.ToString());

        // process score sheet subs
        var repo = new Lo30Repository(context, lo30ContextService);
        var results = repo.ProcessScoreSheetEntrySubs(startingGameIdToProcess, endingGameIdToProcess);
        Print("ProcessScoreSheets.ProcessScoreSheetEntrySubs Modified:" + results.modified + " Time:" + results.time);
        #endregion

        if (playoffToProcess)
        {
          var gameRostersResult = new LoggerResult("GameRosters");

          using (SqlConnection conn = new SqlConnection(_lo30ReportingDBConnString))
          {
            using (SqlCommand cmd = new SqlCommand("ProcessScoreSheetEntrySubsProcessedIntoGameRosters", conn))
            {
              cmd.CommandType = CommandType.StoredProcedure;

              cmd.Parameters.Add("@StartingGameId", SqlDbType.Int).Value = startingGameIdToProcess;
              cmd.Parameters.Add("@EndingGameId", SqlDbType.Int).Value = endingGameIdToProcess;
              cmd.Parameters.Add("@DryRun", SqlDbType.Int).Value = 0;

              conn.Open();
              using (SqlDataReader rdr = cmd.ExecuteReader())
              {
                while (rdr.Read())
                {
                  int newRecordsInserted = Convert.ToInt32(rdr["NewRecordsInserted"]);
                  int existingRecordsUpdated = Convert.ToInt32(rdr["ExistingRecordsUpdated"]);
                  int processedRecordsMatchExistingRecords = Convert.ToInt32(rdr["ProcessedRecordsMatchExistingRecords"]);
                  gameRostersResult.ProcessedCount = processedRecordsMatchExistingRecords + existingRecordsUpdated + newRecordsInserted;
                  gameRostersResult.SavedOrUpdatedCount = existingRecordsUpdated + newRecordsInserted;
                  gameRostersResult.End();
                  _logger.Results.Add(gameRostersResult);
                  Print("GameRosters Count:" + context.GameRosters.Count() + " Processed:" + gameRostersResult.ProcessedCount + " SaveOrUpdated:" + gameRostersResult.SavedOrUpdatedCount);
                }
              }
            }
          }
        }
        diffFromFirst = DateTime.Now - first;
        Print("LoadNewData: TimeToProcess: " + diffFromFirst.ToString());
      }
    }

    private static void Seed()
    {
      string appDataPath = @"C:\git\LO30\LO30\App_Data\";
      var folderPath = Path.Combine(appDataPath, "Access");
      folderPath = folderPath + @"\";

      using (var context = new Lo30Context())
      {
        var _lo30ContextService = new Lo30ContextService(context);

        DateTime first = DateTime.Now;
        DateTime last = DateTime.Now;
        TimeSpan diffFromFirst = new TimeSpan();
        TimeSpan diffFromLast = new TimeSpan();

        #region not populated for now

        #region 0:Addresses
        if (context.Addresses.Count() == 0)
        {
        }
        #endregion

        #region 0:Emails
        if (context.Emails.Count() == 0)
        {
        }
        #endregion

        #region 0:Phones
        if (context.Phones.Count() == 0)
        {
        }
        #endregion

        #region 0:PlayerEmails
        if (context.PlayerEmails.Count() == 0)
        {
        }
        #endregion

        #region 0:PlayerPhones
        if (context.PlayerPhones.Count() == 0)
        {
        }
        #endregion

        #region 0:PlayerStatsCareer
        if (context.PlayerStatsCareer.Count() == 0)
        {
        }
        #endregion

        #region 0:PlayerStatsGame
        if (context.PlayerStatsGame.Count() == 0)
        {
        }
        #endregion

        #region 0:PlayerStatsSeason
        if (context.PlayerStatsSeason.Count() == 0)
        {
        }
        #endregion

        #region 0:Sponsors
        if (context.Sponsors.Count() == 0)
        {
        }
        #endregion

        #region 0:SponsorEmails
        if (context.SponsorEmails.Count() == 0)
        {
        }
        #endregion

        #region 0:SponsorPhones
        if (context.SponsorPhones.Count() == 0)
        {
        }
        #endregion

        #endregion

        #region 1:Articles
        if (context.Articles.Count() == 0)
        {
          Print("Data Group 1: Creating Articles");
          last = DateTime.Now;

          var article = new Article()
          {
            Title = "Payment Schedule",
            Created = new DateTime(2014, 09, 10, 20, 5, 32),
            Body = "Payments are due the Third Thursday of every month as follows:",
          };

          context.Articles.Add(article);

          article = new Article()
          {
            Title = "Game Schedule",
            Created = new DateTime(2014, 09, 8, 15, 12, 41),
            Body = "The schedule has been updated and posted"
          };

          context.Articles.Add(article);

          Print("Data Group 1: Created Articles");
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());

          _lo30ContextService.ContextSaveChanges();
          Print("Data Group 1: Saved Articles" + context.PlayerStatusTypes.Count());
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());
        }
        #endregion

        #region 1:EmailTypes
        if (context.EmailTypes.Count() == 0)
        {
          Print("Data Group 1: Creating EmailTypes");
          last = DateTime.Now;

          var emailType = new EmailType()
          {
            EmailTypeName = "work"
          };

          context.EmailTypes.Add(emailType);

          emailType = new EmailType()
          {
            EmailTypeName = "home"
          };

          context.EmailTypes.Add(emailType);

          emailType = new EmailType()
          {
            EmailTypeName = "other"
          };

          context.EmailTypes.Add(emailType);

          Print("Data Group 1: Created EmailTypes");
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());

          _lo30ContextService.ContextSaveChanges();
          Print("Data Group 1: Saved EmailTypes" + context.EmailTypes.Count());
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());
        }
        #endregion

        #region 1:Penalties
        if (context.Penalties.Count() == 0)
        {
          Print("Data Group 1: Creating Penalties");
          last = DateTime.Now;

          dynamic parsedJson = _accessDatabaseService.ParseObjectFromJsonFile(Path.Combine(folderPath, "Penalties.json"));
          int count = parsedJson.Count;

          Print("Access records to process:" + count);

          for (var d = 0; d < parsedJson.Count; d++)
          {
            if (d % 100 == 0) { Print("Access records processed:" + d); }
            var json = parsedJson[d];

            var penalty = new Penalty()
            {
              PenaltyId = json["PENALTY_ID"],
              PenaltyCode = json["PENALTY_SHORT_DESC"],
              PenaltyName = json["PENALTY_LONG_DESC"],
              DefaultPenaltyMinutes = json["DEFAULT_PENALTY_MINUTES"],
              StickPenalty = json["STICK_PENALTY"]
            };

            context.Penalties.Add(penalty);
          }

          Print("Data Group 1: Created Penalties");
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());

          _lo30ContextService.ContextSaveChanges();
          Print("Data Group 1: Saved Penalties" + context.Penalties.Count());
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());
        }

        #endregion

        #region 1:PhoneTypes
        if (context.PhoneTypes.Count() == 0)
        {
          Print("Data Group 1: Creating PhoneTypes");
          last = DateTime.Now;

          var phoneType = new PhoneType()
          {
            PhoneTypeName = "work"
          };

          context.PhoneTypes.Add(phoneType);

          phoneType = new PhoneType()
          {
            PhoneTypeName = "home"
          };

          context.PhoneTypes.Add(phoneType);

          phoneType = new PhoneType()
          {
            PhoneTypeName = "cell"
          };

          context.PhoneTypes.Add(phoneType);

          phoneType = new PhoneType()
          {
            PhoneTypeName = "other"
          };

          context.PhoneTypes.Add(phoneType);

          Print("Data Group 1: Created PhoneTypes");
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());

          _lo30ContextService.ContextSaveChanges();
          Print("Data Group 1: Saved PhoneTypes" + context.PhoneTypes.Count());
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());
        }
        #endregion

        #region 1:PlayerStatusTypes
        if (context.PlayerStatusTypes.Count() == 0)
        {
          Print("Data Group 1: Creating PlayerStatusTypes");
          last = DateTime.Now;

          dynamic parsedJson = _accessDatabaseService.ParseObjectFromJsonFile(folderPath + "Statuses.json");
          int count = parsedJson.Count;

          Print("Access records to process:" + count);

          for (var d = 0; d < parsedJson.Count; d++)
          {
            if (d % 100 == 0) { Print("Access records processed:" + d); }
            var json = parsedJson[d];

            var playerStatusType = new PlayerStatusType()
            {
              PlayerStatusTypeId = json["STATUS_ID"],
              PlayerStatusTypeName = json["STATUS_DESC"]
            };

            context.PlayerStatusTypes.Add(playerStatusType);
          }

          Print("Data Group 1: Created PlayerStatusTypes");
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());

          _lo30ContextService.ContextSaveChanges();
          Print("Data Group 1: Saved PlayerStatusTypes " + context.PlayerStatusTypes.Count());
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());
        }
        #endregion

        #region 2:Seasons (not using function)
        if (context.Seasons.Count() == 0)
        {
          Print("Data Group 2: Creating Seasons");
          last = DateTime.Now;

          dynamic parsedJson = _accessDatabaseService.ParseObjectFromJsonFile(folderPath + "Seasons.json");
          int count = parsedJson.Count;

          Print("Access records to process:" + count);

          for (var d = 0; d < parsedJson.Count; d++)
          {
            if (d % 100 == 0) { Print("Access records processed:" + d); }
            var json = parsedJson[d];

            DateTime? startDate = null;
            DateTime? endDate = null;

            if (json["START_DATE"] != null)
            {
              startDate = json["START_DATE"];
            }

            if (json["END_DATE"] != null)
            {
              endDate = json["END_DATE"];
            }

            int seasonId = json["SEASON_ID"];

            if (seasonId == 54)
            {
              startDate = new DateTime(2014, 9, 4);
              endDate = new DateTime(2015, 3, 29);
            }

            var season = new Season()
            {
              SeasonId = seasonId,
              SeasonName = json["SEASON_NAME"],
              IsCurrentSeason = json["CURRENT_SEASON_IND"],
              StartYYYYMMDD = _lo30DataService.ConvertDateTimeIntoYYYYMMDD(startDate, ifNullReturnMax: false),
              EndYYYYMMDD = _lo30DataService.ConvertDateTimeIntoYYYYMMDD(endDate, ifNullReturnMax: true)
            };

            context.Seasons.Add(season);
          }

          Print("Data Group 2: Created Seasons");
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());

          _lo30ContextService.ContextSaveChanges();
          Print("Data Group 2: Saved Seasons " + context.Seasons.Count());
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());
        }
        #endregion

        #region 2:Teams
        if (context.Teams.Count() == 0)
        {
          Print("Data Group 2: Creating Teams");
          last = DateTime.Now;

          #region add position night teams
          var team = new Team()
          {
            TeamShortName = "1st",
            TeamLongName = "1st Place Team Placeholder"
          };
          context.Teams.Add(team);

          team = new Team()
          {
            TeamShortName = "2nd",
            TeamLongName = "2nd Place Team Placeholder"
          };
          context.Teams.Add(team);

          team = new Team()
          {
            TeamShortName = "3rd",
            TeamLongName = "3rd Place Team Placeholder"
          };
          context.Teams.Add(team);

          team = new Team()
          {
            TeamShortName = "4th",
            TeamLongName = "4th Place Team Placeholder"
          };
          context.Teams.Add(team);

          team = new Team()
          {
            TeamShortName = "5th",
            TeamLongName = "5th Place Team Placeholder"
          };
          context.Teams.Add(team);

          team = new Team()
          {
            TeamShortName = "6th",
            TeamLongName = "6th Place Team Placeholder"
          };
          context.Teams.Add(team);

          team = new Team()
          {
            TeamShortName = "7th",
            TeamLongName = "7th Place Team Placeholder"
          };
          context.Teams.Add(team);

          team = new Team()
          {
            TeamShortName = "8th",
            TeamLongName = "8th Place Team Placeholder"
          };
          context.Teams.Add(team);

          team = new Team()
          {
            TeamShortName = "9th",
            TeamLongName = "9th Place Team Placeholder"
          };
          context.Teams.Add(team);

          team = new Team()
          {
            TeamShortName = "10th",
            TeamLongName = "10th Place Team Placeholder"
          };
          context.Teams.Add(team);

          team = new Team()
          {
            TeamShortName = "11th",
            TeamLongName = "11th Place Team Placeholder"
          };
          context.Teams.Add(team);

          team = new Team()
          {
            TeamShortName = "12th",
            TeamLongName = "12th Place Team Placeholder"
          };
          context.Teams.Add(team);

          team = new Team()
          {
            TeamShortName = "13th",
            TeamLongName = "13th Place Team Placeholder"
          };
          context.Teams.Add(team);

          team = new Team()
          {
            TeamShortName = "14th",
            TeamLongName = "14th Place Team Placeholder"
          };
          context.Teams.Add(team);

          team = new Team()
          {
            TeamShortName = "15th",
            TeamLongName = "15th Place Team Placeholder"
          };
          context.Teams.Add(team);

          team = new Team()
          {
            TeamShortName = "16th",
            TeamLongName = "16th Place Team Placeholder"
          };
          context.Teams.Add(team);
          #endregion

          var sql = "SELECT DISTINCT TEAM_SHORT_NAME, TEAM_LONG_NAME FROM TEAM";

          dynamic parsedJson = _accessDatabaseService.ParseObjectFromJsonFile(folderPath + "Teams.json");
          int count = parsedJson.Count;

          Print("Access records to process:" + count);

          for (var d = 0; d < parsedJson.Count; d++)
          {
            if (d % 100 == 0) { Print("Access records processed:" + d); }
            var json = parsedJson[d];

            team = new Team()
            {
              TeamShortName = json["TEAM_SHORT_NAME"],
              TeamLongName = json["TEAM_LONG_NAME"]
            };

            context.Teams.Add(team);
          }

          Print("Data Group 2: Created Teams");
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());

          _lo30ContextService.ContextSaveChanges();
          Print("Data Group 2: Saved Teams " + context.Teams.Count());
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());
        }
        #endregion

        #region 2:Divisions
        if (context.Divisions.Count() == 0)
        {
          Print("Data Group 2: Creating Divisions");
          last = DateTime.Now;
          int saveOrUpdatedCount = 0;

          var division = new Division(did: 0, dln: "No Division", dsn: "n/a");
          saveOrUpdatedCount = +_lo30ContextService.SaveOrUpdateDivision(division);

          dynamic parsedJson = _accessDatabaseService.ParseObjectFromJsonFile(folderPath + "Teams.json");
          int count = parsedJson.Count;

          Print("Access records to process:" + count);

          for (var d = 0; d < parsedJson.Count; d++)
          {
            if (d % 100 == 0) { Print("Access records processed:" + d); }
            var json = parsedJson[d];

            string divName = json["TEAM_DIVISION_NAME"];

            if (!string.IsNullOrWhiteSpace(divName))
            {
              var found = _lo30ContextService.FindDivisionByPK2(divName, errorIfNotFound: false, errorIfMoreThanOneFound: true, populateFully: false);
              if (found == null)
              { // only add new divisions
                division = new Division()
                {
                  DivisionLongName = divName,
                  DivisionShortName = "TBD"
                };
                saveOrUpdatedCount = +_lo30ContextService.SaveOrUpdateDivision(division);
              }
            }
          }

          Print("Data Group 2: Saved or updated Divisions " + saveOrUpdatedCount);
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());
        }

        #endregion

        #region 2:Players (using function)
        if (context.Players.Count() == 0)
        {
          _logger.Results.Add(_playersProcessor.SaveOrUpdatePlayers(context, _lo30ContextService, folderPath));
        }
        #endregion

        #region 3:SeasonTeam
        if (context.SeasonTeams.Count() == 0)
        {
          Print("Data Group 3: Creating SeasonTeams");
          last = DateTime.Now;

          #region add the position night placeholders for this season
          var team = context.Teams.Where(t => t.TeamShortName == "1st").FirstOrDefault();
          var divId = 1;
          var seasonTeam = new SeasonTeam(stid: 321, sid: 54, tid: team.TeamId, div: divId);
          context.SeasonTeams.Add(seasonTeam);

          team = context.Teams.Where(t => t.TeamShortName == "2nd").FirstOrDefault();
          seasonTeam = new SeasonTeam(stid: 322, sid: 54, tid: team.TeamId, div: divId);
          context.SeasonTeams.Add(seasonTeam);

          team = context.Teams.Where(t => t.TeamShortName == "3rd").FirstOrDefault();
          seasonTeam = new SeasonTeam(stid: 323, sid: 54, tid: team.TeamId, div: divId);
          context.SeasonTeams.Add(seasonTeam);

          team = context.Teams.Where(t => t.TeamShortName == "4th").FirstOrDefault();
          seasonTeam = new SeasonTeam(stid: 324, sid: 54, tid: team.TeamId, div: divId);
          context.SeasonTeams.Add(seasonTeam);

          team = context.Teams.Where(t => t.TeamShortName == "5th").FirstOrDefault();
          seasonTeam = new SeasonTeam(stid: 325, sid: 54, tid: team.TeamId, div: divId);
          context.SeasonTeams.Add(seasonTeam);

          team = context.Teams.Where(t => t.TeamShortName == "6th").FirstOrDefault();
          seasonTeam = new SeasonTeam(stid: 326, sid: 54, tid: team.TeamId, div: divId);
          context.SeasonTeams.Add(seasonTeam);

          team = context.Teams.Where(t => t.TeamShortName == "7th").FirstOrDefault();
          seasonTeam = new SeasonTeam(stid: 327, sid: 54, tid: team.TeamId, div: divId);
          context.SeasonTeams.Add(seasonTeam);

          team = context.Teams.Where(t => t.TeamShortName == "8th").FirstOrDefault();
          seasonTeam = new SeasonTeam(stid: 328, sid: 54, tid: team.TeamId, div: divId);
          context.SeasonTeams.Add(seasonTeam);
          #endregion

          dynamic parsedJson = _accessDatabaseService.ParseObjectFromJsonFile(folderPath + "Teams.json");
          int count = parsedJson.Count;

          Print("Access records to process:" + count);

          for (var d = 0; d < parsedJson.Count; d++)
          {
            if (d % 100 == 0) { Print("Access records processed:" + d); }
            var json = parsedJson[d];

            string teamShortName = json["TEAM_SHORT_NAME"];

            team = context.Teams.Where(t => t.TeamShortName == teamShortName).FirstOrDefault();

            int stid = json["TEAM_ID"];
            int sid = json["SEASON_ID"];
            string div = json["TEAM_DIVISION_NAME"];
            int tid = team.TeamId;

            divId = 1;
            if (!string.IsNullOrWhiteSpace(div))
            {
              var division = context.Divisions.Where(x => x.DivisionLongName == div).FirstOrDefault();
              divId = division.DivisionId;
            }
            seasonTeam = new SeasonTeam(stid, sid, tid, divId);

            context.SeasonTeams.Add(seasonTeam);
          }

          Print("Data Group 3: Created SeasonTeams");
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());

          _lo30ContextService.ContextSaveChanges();
          Print("Data Group 3: Saved SeasonTeams " + context.SeasonTeams.Count());
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());
        }
        #endregion

        #region 3:Games (using function)
        if (context.Games.Count() == 0)
        {
          Print("Data Group 3: Creating Games");
          last = DateTime.Now;

          SaveOrUpdateGames(context, _lo30ContextService, folderPath, startingGameIdToProcess: 3200, endingGameIdToProcess: 3319);  // sid 54, pfs:false
          SaveOrUpdateGames(context, _lo30ContextService, folderPath, startingGameIdToProcess: 3324, endingGameIdToProcess: 3377);  // sid 54, pfs:true

          Print("Data Group 3: Saved Games " + context.Games.Count());
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());
        }
        #endregion

        #region 3:GameTeams (using function)
        if (context.GameTeams.Count() == 0)
        {
          _logger.Results.Add(_gameTeamsProcessor.SaveOrUpdateGameTeams(context, _lo30ContextService, folderPath, startingGameIdToProcess: 3200, endingGameIdToProcess: 3319));  // sid 54, pfs:false
          _logger.Results.Add(_gameTeamsProcessor.SaveOrUpdateGameTeams(context, _lo30ContextService, folderPath, startingGameIdToProcess: 3324, endingGameIdToProcess: 3377));  // sid 54, pfs:true
        }
        #endregion

        #region 3:PlayerStatuses
        if (context.PlayerStatuses.Count() == 0)
        {
          Print("Data Group 3: Creating PlayerStatuses");
          last = DateTime.Now;

          dynamic parsedJson = _accessDatabaseService.ParseObjectFromJsonFile(folderPath + "PlayerStatuses.json");
          int count = parsedJson.Count;

          Print("Access records to process:" + count);

          for (var d = 0; d < parsedJson.Count; d++)
          {
            if (d % 100 == 0) { Print("Access records processed:" + d); }
            var json = parsedJson[d];

            int playerId = json["PLAYER_ID"];

            if (playerId == 512 || playerId == 545 || playerId == 571 || playerId == 170 || playerId == 211 || playerId == 213 || playerId == 215 || playerId == 217 || playerId == 282 || playerId == 381 || playerId == 426 || playerId == 432 || playerId == 767)
            {
              // do nothing, these guys do not have a player record
            }
            else
            {
              DateTime? startDate = json["START_DATE"];
              DateTime? endDate = json["END_DATE"];
              int startYYYYMMDD = _lo30DataService.ConvertDateTimeIntoYYYYMMDD(startDate, ifNullReturnMax: false);
              int endYYYYMMDD = _lo30DataService.ConvertDateTimeIntoYYYYMMDD(endDate, ifNullReturnMax: true);

              var playerStatus = new PlayerStatus()
              {
                PlayerId = playerId,
                PlayerStatusTypeId = json["STATUS_ID"],
                StartYYYYMMDD = startYYYYMMDD,
                EndYYYYMMDD = endYYYYMMDD,
                CurrentStatus = false
              };

              context.PlayerStatuses.Add(playerStatus);
            }
          }

          Print("Data Group 3: Created PlayerStatuses");
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());

          _lo30ContextService.ContextSaveChanges();
          Print("Data Group 3: Saved PlayerStatuses " + context.PlayerStatuses.Count());
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());

          // determine the current status
          var currentPlayerStatus = context.PlayerStatuses
                                              .GroupBy(x => new { x.PlayerId })
                                              .Select(grp => new
                                              {
                                                PlayerId = grp.Key.PlayerId,
                                                EndYYYYMMDD = grp.Max(x => x.EndYYYYMMDD)
                                              })
                                              .ToList();

          // now update those records
          foreach (var current in currentPlayerStatus)
          {
            var playerStatus = context.PlayerStatuses.Where(x => x.PlayerId == current.PlayerId && x.EndYYYYMMDD == current.EndYYYYMMDD).FirstOrDefault();
            playerStatus.CurrentStatus = true;
          }

          int updated = _lo30ContextService.ContextSaveChanges();
          Print("Data Group 3: Updated PlayerStatuses Current " + updated);
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());
        }
        #endregion

        #region 3:PlayerDrafts
        if (context.PlayerDrafts.Count() == 0)
        {
          Print("Data Group 3: Creating PlayerDrafts");
          last = DateTime.Now;

          dynamic parsedJson = _accessDatabaseService.ParseObjectFromJsonFile(folderPath + "PlayerRatings.json");
          int count = parsedJson.Count;

          Print("Access records to process:" + count);

          for (var d = 0; d < parsedJson.Count; d++)
          {
            if (d % 100 == 0) { Print("Access records processed:" + d); }
            var json = parsedJson[d];

            int seasonId = json["SEASON_ID"];
            int playerId = json["PLAYER_ID"];

            string draftRound = "";
            string position = "";
            if (json["PLAYER_DRAFT_ROUND"] != null)
            {
              draftRound = json["PLAYER_DRAFT_ROUND"];
            }

            int round = -1;
            int line = -1;

            switch (draftRound.ToLower())
            {
              case "g":
              case "1g":
              case "2g":
              case "3g":
              case "4g":
              case "5g":
              case "6g":
              case "7g":
              case "8g":
                position = "G";
                line = 1;
                round = 1;
                break;
              case "1d":
                position = "D";
                line = 1;
                round = 2;
                break;
              case "2d":
                position = "D";
                line = 1;
                round = 5;
                break;
              case "3d":
                position = "D";
                line = 2;
                round = 8;
                break;
              case "4d":
                position = "D";
                line = 2;
                round = 11;
                break;
              case "5d":
                position = "D";
                line = 3;
                break;
              case "6d":
                position = "D";
                line = 3;
                round = 14;
                break;
              case "1f":
                position = "F";
                line = 1;
                round = 3;
                break;
              case "2f":
                position = "F";
                line = 1;
                round = 4;
                break;
              case "3f":
                position = "F";
                line = 1;
                round = 6;
                break;
              case "4f":
                position = "F";
                line = 2;
                round = 7;
                break;
              case "5f":
                position = "F";
                line = 2;
                round = 9;
                break;
              case "6f":
                position = "F";
                line = 2;
                round = 10;
                break;
              case "7f":
                position = "F";
                line = 3;
                round = 12;
                break;
              case "8f":
                position = "F";
                line = 3;
                round = 13;
                break;
              case "9f":
                position = "F";
                line = 3;
                round = 15;
                break;
            }
            if (playerId == 545 || playerId == 512 || playerId == 426 || playerId == 432 || playerId == 381 || playerId == 282)
            {
              // skip these players...they do not exist in the players table
            }
            else
            {
              var playerDraft = new PlayerDraft()
              {
                SeasonId = seasonId,
                PlayerId = playerId,
                Round = round,
                Order = -1,
                Position = position,
                Line = line,
                Special = false
              };

              context.PlayerDrafts.Add(playerDraft);
            }
          }

          Print("Data Group 3: Created PlayerDrafts");
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());

          _lo30ContextService.ContextSaveChanges();
          Print("Data Group 3: Saved PlayerDrafts " + context.PlayerDrafts.Count());
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());
        }
        #endregion

        #region 3:PlayerRatings...dependency on Season, Players, and PlayerDrafts
        if (context.PlayerRatings.Count() == 0)
        {
          Print("Data Group 3: Creating PlayerRatings");
          last = DateTime.Now;

          dynamic parsedJson = _accessDatabaseService.ParseObjectFromJsonFile(folderPath + "PlayerRatings.json");
          int count = parsedJson.Count;

          Print("Access records to process:" + count);

          for (var d = 0; d < parsedJson.Count; d++)
          {
            if (d % 100 == 0) { Print("Access records processed:" + d); }
            var json = parsedJson[d];

            string[] ratingParts = new string[0];

            if (json["PLAYER_RATING"] != null)
            {
              string rating = json["PLAYER_RATING"];
              ratingParts = rating.Split('.');
            }

            int ratingPrimary = -1;
            int ratingSecondary = -1;
            if (ratingParts.Length > 0)
            {
              ratingPrimary = Convert.ToInt32(ratingParts[0]);
              ratingSecondary = 0;
              if (ratingParts.Length > 1)
              {
                ratingSecondary = Convert.ToInt32(ratingParts[1]);
              }
            }

            int line = 0;
            if (json["PLAYER_LINE"] != null)
            {
              line = json["PLAYER_LINE"];
            }

            int seasonId = json["SEASON_ID"];
            int playerId = json["PLAYER_ID"];

            if (playerId == 545 || playerId == 512 || playerId == 426 || playerId == 432 || playerId == 381 || playerId == 282)
            {
              // skip these players...they do not exist in the players table
            }
            else
            {

              var season = _lo30ContextService.FindSeason(seasonId);
              var playerDraft = _lo30ContextService.FindPlayerDraft(seasonId, playerId);

              // default the players rating to the start/end of the season
              var playerRating = new PlayerRating(
                                        sid: seasonId,
                                        pid: playerId,
                                        symd: season.StartYYYYMMDD,
                                        eymd: season.EndYYYYMMDD,
                                        rp: ratingPrimary,
                                        rs: ratingSecondary,
                                        line: line
                                        );

              context.PlayerRatings.Add(playerRating);
            }
          }

          Print("Data Group 3: Created PlayerRatings");
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());

          _lo30ContextService.ContextSaveChanges();
          Print("Data Group 3: Saved PlayerRatings " + context.PlayerRatings.Count());
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());

          // add missing players with default rating (0.0)

          var players = context.Players.ToList();

          foreach (var player in players)
          {
            var found = context.PlayerRatings.Where(x => x.PlayerId == player.PlayerId).ToList();

            if (found == null || found.Count == 0)
            {
              // TODO, remove hard coding
              var season = _lo30ContextService.FindSeason(54);

              // default the players rating to the start/end of the season
              var playerRating = new PlayerRating(
                                        sid: season.SeasonId,
                                        pid: player.PlayerId,
                                        symd: season.StartYYYYMMDD,
                                        eymd: season.EndYYYYMMDD
                                        );

              context.PlayerRatings.Add(playerRating);
            }
          }
        }
        #endregion

        #region 3:TeamRosters (using function)
        if (context.TeamRosters.Count() == 0)
        {
          Print("Data Group 3: Creating TeamRosters");
          last = DateTime.Now;

          SaveOrUpdateTeamRosters(context, _lo30ContextService, folderPath, seasonIdToProcess: 54, playoffToProcess: false);
          SaveOrUpdateTeamRosters(context, _lo30ContextService, folderPath, seasonIdToProcess: 54, playoffToProcess: true);

          Print("Data Group 3: Saved TeamRosters " + context.TeamRosters.Count());
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());
        }
        #endregion

        #region 4:GameRosters (using function)
        if (context.GameRosters.Count() == 0)
        {
          _logger.Results.Add(_gameRostersProcessor.SaveOrUpdateGameRosters(context, _lo30ContextService, folderPath, startingGameIdToProcess: 3200, endingGameIdToProcess: 3319)); // sid 54, pfs:false
          _logger.Results.Add(_gameRostersProcessor.SaveOrUpdateGameRosters(context, _lo30ContextService, folderPath, startingGameIdToProcess: 3324, endingGameIdToProcess: 3377));  // sid 54, pfs:true
        }
        #endregion

        #region 4:ScoreSheetEntries...using loadJson
        if (context.ScoreSheetEntries.Count() == 0)
        {
          List<ScoreSheetEntry> scoreSheetEntries = ScoreSheetEntry.LoadListFromAccessDbJsonFile(folderPath + "ScoreSheetEntries.json", 0, 999999);
          int countSaveOrUpdatedSSE = _lo30ContextService.SaveOrUpdateScoreSheetEntry(scoreSheetEntries);
          Print("Data Group 4: ScoreSheetEntries Count:" + context.ScoreSheetEntries.Count() + " SaveOrUpdated:" + countSaveOrUpdatedSSE);
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());
        }
        #endregion

        #region 4:ScoreSheetEntryPenalties...using loadJson
        if (context.ScoreSheetEntryPenalties.Count() == 0)
        {
          List<ScoreSheetEntryPenalty> scoreSheetEntryPenalties = ScoreSheetEntryPenalty.LoadListFromAccessDbJsonFile(folderPath + "ScoreSheetEntryPenalties.json", 0, 999999);
          int countSaveOrUpdatedSSEP = _lo30ContextService.SaveOrUpdateScoreSheetEntryPenalty(scoreSheetEntryPenalties);
          Print("Data Group 4: ScoreSheetEntryPenalties Count:" + context.ScoreSheetEntryPenalties.Count() + " SaveOrUpdated:" + countSaveOrUpdatedSSEP);
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());
        }
        #endregion

        #region 4:ScoreSheetEntrySubs...using loadJson
        if (context.ScoreSheetEntrySubs.Count() == 0)
        {
          List<ScoreSheetEntrySub> scoreSheetEntrySubs = ScoreSheetEntrySub.LoadListFromAccessDbJsonFile(folderPath + "ScoreSheetEntrySubs.json", 0, 999999);
          int countSaveOrUpdatedSSES = _lo30ContextService.SaveOrUpdateScoreSheetEntrySub(scoreSheetEntrySubs);
          Print("Data Group 4: ScoreSheetEntrySubs Count:" + context.ScoreSheetEntrySubs.Count() + " SaveOrUpdated:" + countSaveOrUpdatedSSES);
          diffFromLast = DateTime.Now - last;
          Print("TimeToProcess: " + diffFromLast.ToString());
        }
        #endregion

        #region populated via other processes
        #region 99:GameOutcomes
        if (context.GameOutcomes.Count() == 0)
        {
          // populated via other process
        }
        #endregion

        #region 99:TeamStandings
        if (context.TeamStandings.Count() == 0)
        {
          // populated via other process
        }
        #endregion
        #endregion

        #region create sql views
        string connString = System.Configuration.ConfigurationManager.ConnectionStrings["LO30ReportingDB"].ConnectionString;

        var viewFileNameList = new List<string>(){
          "TeamRostersView.sql"
        };

        foreach (var viewFileName in viewFileNameList)
        {
          var viewFullFilePath = Path.Combine(appDataPath, @"SqlServer\Views");
          viewFullFilePath = Path.Combine(viewFullFilePath, viewFileName);
          string viewSql = File.ReadAllText(viewFullFilePath);
          using (SqlConnection connection = new SqlConnection(connString))
          {
            // first drop the view
            var viewName = "dbo." + viewFileName.Replace(".sql", "");
            var dropSql = "IF OBJECT_ID('" + viewName + "', 'V') IS NOT NULL DROP VIEW " + viewName;
            SqlCommand command = new SqlCommand(dropSql, connection);
            command.Connection.Open();
            command.ExecuteNonQuery();

            command = new SqlCommand(viewSql, connection);
            command.ExecuteNonQuery();
          }
        }

        #endregion

        diffFromFirst = DateTime.Now - first;
        Print("Total TimeToProcess: " + diffFromFirst.ToString());
      }
    }

    private static void SaveOrUpdateGames(Lo30Context context, Lo30ContextService lo30ContextService, string folderPath, int startingGameIdToProcess, int endingGameIdToProcess)
    {
      Print("SaveOrUpdateGames: Starting");
      var last = DateTime.Now;

      dynamic parsedJson = _accessDatabaseService.ParseObjectFromJsonFile(folderPath + "Games.json");
      int count = parsedJson.Count;

      Print("SaveOrUpdateGames:Access records to process:" + count);

      int countSaveOrUpdated = 0;
      for (var d = 0; d < parsedJson.Count; d++)
      {
        if (d % 100 == 0) { Print("SaveOrUpdateGames:Access records processed:" + d); }
        var json = parsedJson[d];

        int gameId = json["GAME_ID"];
        if (gameId >= startingGameIdToProcess && gameId <= endingGameIdToProcess)
        {
          int seasonId = json["SEASON_ID"];
          DateTime gameDate = json["GAME_DATE"];
          DateTime gameTime = json["GAME_TIME"];
          bool playoffGame = json["PLAYOFF_GAME_IND"];

          var timeSpan = new TimeSpan(gameTime.Hour, gameTime.Minute, gameTime.Second);

          var gameDateTime = gameDate.Add(timeSpan);

          var game = new Game(
                gid: gameId,
                time: gameDateTime,
                loc: "not set",
                sid: seasonId,
                play: playoffGame
          );

          //context.Games.Add(game);  // works only if never reprocessing data

          countSaveOrUpdated = countSaveOrUpdated + lo30ContextService.SaveOrUpdateGame(game);
        }
      }

      //Print("SaveOrUpdateGames: Created Games");
      //var diffFromLast = DateTime.Now - last;
      //Print("SaveOrUpdateGames: TimeToProcess: " + diffFromLast.ToString());

      //var countSaveOrUpdated = lo30ContextService.ContextSaveChanges(); // works only if never reprocessing data
      Print("SaveOrUpdateGames: Games Count: " + context.Games.Count() + " SaveOrUpdated:" + countSaveOrUpdated);
      var diffFromLast = DateTime.Now - last;
      Print("SaveOrUpdateGames: TimeToProcess: " + diffFromLast.ToString());
    }

    private static void SaveOrUpdateSeasons(Lo30Context context, Lo30ContextService lo30ContextService, string folderPath, int seasonIdToProcess)
    {
      Print("SaveOrUpdateSeasons: Creating Seasons");
      var last = DateTime.Now;

      dynamic parsedJson = _accessDatabaseService.ParseObjectFromJsonFile(folderPath + "Seasons.json");
      int count = parsedJson.Count;

      Print("SaveOrUpdateSeasons: Access records to process:" + count);

      int countSaveOrUpdated = 0;
      for (var d = 0; d < parsedJson.Count; d++)
      {
        if (d % 100 == 0) { Print("SaveOrUpdateSeasons: Access records processed:" + d); }
        var json = parsedJson[d];

        int seasonId = json["SEASON_ID"];

        if (seasonId == seasonIdToProcess)
        {
          DateTime? startDate = null;
          DateTime? endDate = null;

          if (json["START_DATE"] != null)
          {
            startDate = json["START_DATE"];
          }

          if (json["END_DATE"] != null)
          {
            endDate = json["END_DATE"];
          }

          if (seasonId == 54)
          {
            startDate = new DateTime(2014, 9, 4);
            endDate = new DateTime(2015, 3, 22);
          }

          var season = new Season()
          {
            SeasonId = seasonId,
            SeasonName = json["SEASON_NAME"],
            IsCurrentSeason = json["CURRENT_SEASON_IND"],
            StartYYYYMMDD = _lo30DataService.ConvertDateTimeIntoYYYYMMDD(startDate, ifNullReturnMax: false),
            EndYYYYMMDD = _lo30DataService.ConvertDateTimeIntoYYYYMMDD(endDate, ifNullReturnMax: true)
          };

          countSaveOrUpdated = countSaveOrUpdated + lo30ContextService.SaveOrUpdateSeason(season);
        }
      }

      lo30ContextService.ContextSaveChanges();
      Print("SaveOrUpdateSeasons: Seasons Count:" + context.Seasons.Count() + " SaveOrUpdated:" + countSaveOrUpdated);
      var diffFromLast = DateTime.Now - last;
      Print("SaveOrUpdateSeasons: TimeToProcess: " + diffFromLast.ToString());
    }

    private static void SaveOrUpdateTeamRosters(Lo30Context context, Lo30ContextService lo30ContextService, string folderPath, int seasonIdToProcess, bool playoffToProcess)
    {
      Print("SaveOrUpdateTeamRosters: Creating TeamRosters");
      var last = DateTime.Now;

      dynamic parsedJson = _accessDatabaseService.ParseObjectFromJsonFile(folderPath + "TeamRosters.json");
      int count = parsedJson.Count;

      Print("SaveOrUpdateTeamRosters: Access records to process:" + count);

      int countSaveOrUpdated = 0;
      for (var d = 0; d < parsedJson.Count; d++)
      {
        if (d % 100 == 0) { Print("SaveOrUpdateTeamRosters: Access records processed:" + d); }
        var json = parsedJson[d];

        int seasonId = json["SEASON_ID"];
        bool playoff = json["PLAYOFF_SEASON_IND"];

        if (seasonId == seasonIdToProcess && playoff == playoffToProcess)
        {
          int teamId = json["TEAM_ID"];
          int playerId = json["PLAYER_ID"];

          int playerNumber = -1;
          if (json["PLAYER_NUMBER"] != null)
          {
            playerNumber = json["PLAYER_NUMBER"];
          }

          // based on the draft spot, determine team roster line and position
          var seasonTeam = lo30ContextService.FindSeasonTeam(teamId);
          var season = lo30ContextService.FindSeason(seasonId);

          PlayerDraft playerDraft;
          if (seasonTeam.SeasonId == 54 && playoffToProcess == false && playerId == 66)
          {
            // HACK FIX for Bill Hamilton rostered sub
            playerDraft = new PlayerDraft()
            {
              PlayerId = playerId,
              SeasonId = seasonTeam.SeasonId,
              Line = 1,
              Position = "F",
              Order = -1,
              Round = 4,
              Special = false
            };
          }
          else if (seasonTeam.SeasonId == 54 && playoffToProcess == false && playerId == 662)
          {
            // HACK FIX for Matt Spease rostered sub
            playerDraft = new PlayerDraft()
            {
              PlayerId = playerId,
              SeasonId = seasonTeam.SeasonId,
              Line = 2,
              Position = "F",
              Order = -1,
              Round = 10,
              Special = false
            };
          }
          else if (seasonTeam.SeasonId == 54 && playoffToProcess == true && playerId == 674)
          {
            // HACK FIX for Bob Hickson rostered sub
            playerDraft = new PlayerDraft()
            {
              PlayerId = playerId,
              SeasonId = seasonTeam.SeasonId,
              Line = 1,
              Position = "D",
              Order = -1,
              Round = -1,
              Special = false
            };
          }
          else if (seasonTeam.SeasonId == 54 && playoffToProcess == true && playerId == 681)
          {
            // HACK FIX for Geoff Cutsy rostered sub
            playerDraft = new PlayerDraft()
            {
              PlayerId = playerId,
              SeasonId = seasonTeam.SeasonId,
              Line = 1,
              Position = "F",
              Order = -1,
              Round = -1,
              Special = false
            };
          }
          else if (seasonTeam.SeasonId == 54 && playoffToProcess == true && playerId == 757)
          {
            // HACK FIX for Gary Zielke rostered sub
            playerDraft = new PlayerDraft()
            {
              PlayerId = playerId,
              SeasonId = seasonTeam.SeasonId,
              Line = 1,
              Position = "D",
              Order = -1,
              Round = -1,
              Special = false
            };
          }
          else if (seasonTeam.SeasonId == 54 && playoffToProcess == true && playerId == 758)
          {
            // HACK FIX for Paul Fretter rostered sub
            playerDraft = new PlayerDraft()
            {
              PlayerId = playerId,
              SeasonId = seasonTeam.SeasonId,
              Line = 3,
              Position = "F",
              Order = -1,
              Round = -1,
              Special = false
            };
          }
          else if (seasonTeam.SeasonId == 54 && playoffToProcess == true && playerId == 721)
          {
            // HACK FIX for Tom Small rostered sub
            playerDraft = new PlayerDraft()
            {
              PlayerId = playerId,
              SeasonId = seasonTeam.SeasonId,
              Line = 2,
              Position = "D",
              Order = -1,
              Round = -1,
              Special = false
            };
          }
          else if (seasonTeam.SeasonId == 54 && playoffToProcess == true && playerId == 581)
          {
            // HACK FIX for Kyle Krupsky rostered sub
            playerDraft = new PlayerDraft()
            {
              PlayerId = playerId,
              SeasonId = seasonTeam.SeasonId,
              Line = 2,
              Position = "F",
              Order = -1,
              Round = -1,
              Special = false
            };
          }
          else
          {
            playerDraft = lo30ContextService.FindPlayerDraft(seasonTeam.SeasonId, playerId);
          }

          PlayerRating playerRating;
          if (seasonTeam.SeasonId == 54 && playoffToProcess == false && playerId == 66)
          {
            // HACK FIX for Bill Hamilton rostered sub
            playerRating = new PlayerRating()
            {
              PlayerId = playerId,
              SeasonId = seasonTeam.SeasonId,
              RatingPrimary = 2,
              RatingSecondary = 1,
              StartYYYYMMDD = 20140904,
              EndYYYYMMDD = 20141031,
              Line = 1,
              Position = "F"
            };
          }
          else if (seasonTeam.SeasonId == 54 && playoffToProcess == false && playerId == 662)
          {
            // HACK FIX for Matt Spease rostered sub
            playerRating = new PlayerRating()
            {
              PlayerId = playerId,
              SeasonId = seasonTeam.SeasonId,
              RatingPrimary = 6,
              RatingSecondary = 2,
              StartYYYYMMDD = 20140904,
              EndYYYYMMDD = 20141031,
              Line = 2,
              Position = "F"
            };
          }
          else
          {
            playerRating = lo30ContextService.FindPlayerRatingWithYYYYMMDD(playerId, playerDraft.Position, seasonTeam.SeasonId, season.StartYYYYMMDD);
          }

          // default the team roster to the start/end of the season
          TeamRoster teamRoster;
          if (seasonTeam.SeasonId == 54 && playoffToProcess == false && playerId == 710)
          {
            // HACK FIX for Bill Hamilton rostered sub (Howard)

            // add billy
            teamRoster = new TeamRoster(
                    stid: teamId,
                    pid: 66,
                    symd: 20140904,
                    eymd: 20141031,
                    pos: "F",
                    rp: 2,
                    rs: 1,
                    line: 1,
                    pn: 15
            );
            countSaveOrUpdated = countSaveOrUpdated + lo30ContextService.SaveOrUpdateTeamRoster(teamRoster);

            // add howard
            teamRoster = new TeamRoster(
                                    stid: teamId,
                                    pid: playerId,
                                    symd: 20141101,
                                    eymd: 20150118,
                                    pos: playerDraft.Position,
                                    rp: playerRating.RatingPrimary,
                                    rs: playerRating.RatingSecondary,
                                    line: playerDraft.Line,
                                    pn: playerNumber
                            );
            countSaveOrUpdated = countSaveOrUpdated + lo30ContextService.SaveOrUpdateTeamRoster(teamRoster);
          }
          else if (seasonTeam.SeasonId == 54 && playoffToProcess == false && playerId == 708)
          {
            // HACK FIX for Matt Spease rostered sub (Vince)

            // add matt
            teamRoster = new TeamRoster(
                    stid: teamId,
                    pid: 662,
                    symd: 20140904,
                    eymd: 20141031,
                    pos: "F",
                    rp: 6,
                    rs: 2,
                    line: 2,
                    pn: 17
            );
            countSaveOrUpdated = countSaveOrUpdated + lo30ContextService.SaveOrUpdateTeamRoster(teamRoster);

            // add vince
            teamRoster = new TeamRoster(
                                    stid: teamId,
                                    pid: playerId,
                                    symd: 20141101,
                                    eymd: 20150118,
                                    pos: playerDraft.Position,
                                    rp: playerRating.RatingPrimary,
                                    rs: playerRating.RatingSecondary,
                                    line: playerDraft.Line,
                                    pn: playerNumber
                            );
            countSaveOrUpdated = countSaveOrUpdated + lo30ContextService.SaveOrUpdateTeamRoster(teamRoster);
          }
          else
          {
            int startYYYYMMDD, endYYYYMMDD;

            if (
                  seasonId == 54 && playoff == true &&
                  (
                    playerId == 763 || playerId == 64 || playerId == 594 ||
                    playerId == 33 ||
                    playerId == 178 || playerId == 674 ||
                    playerId == 581 ||
                    playerId == 721 ||
                    playerId == 757 || playerId == 758
                  )
              )
            {
              // 763 Scott Ranta, 64 Mark Ranta, 594 BJ to LAB
              // 33 Todd Keller to Zas Ent
              // 178 Ken Grant, 674 Bob Hickson (708 Vince DeMassa...from regular season) to Bill Brown
              // 581 Kyle Krupsky (710 Howard Schoenfeldt...from regular season) to D&G
              // 721 Tom Small to Hunt's Ace
              // 757 Gary Zielke, 758 Paul Fretter to Glover
              // 686 Kris Medico, 681 Geoff Cutsy to DPKZ (Joe/Pete still on same team)
              startYYYYMMDD = 20150122;
              endYYYYMMDD = 20150322;
            }
            else if (
                seasonId == 54 && playoff == true &&
                (
                  playerId == 708 || playerId == 710
                )
            )
            {
              // (708 Vince DeMassa...from regular season) to Bill Brown
              // (710 Howard Schoenfeldt...from regular season) to D&G
              startYYYYMMDD = 20141101;
              endYYYYMMDD = 20150322;
            }
            else if (seasonId == 54 && playoff == false)
            {
              // if roster is before playoffs, we don't know if they will be traded
              // so default to end of regular season date...so if they get traded, don't have to 
              // go back and update the old/initial record
              startYYYYMMDD = 20140904;
              endYYYYMMDD = 20150118;
            }
            else if (seasonId == 54 && playoff == true)
            {
              // if they were not one of the guys who were affected by the trades,
              // change their end date to the end of the year
              startYYYYMMDD = 20140904;
              endYYYYMMDD = 20150322;
            }
            else
            {
              startYYYYMMDD = season.StartYYYYMMDD;
              endYYYYMMDD = season.EndYYYYMMDD;
            }

            teamRoster = new TeamRoster(
                                    stid: teamId,
                                    pid: playerId,
                                    symd: startYYYYMMDD,
                                    eymd: endYYYYMMDD,
                                    pos: playerDraft.Position,
                                    rp: playerRating.RatingPrimary,
                                    rs: playerRating.RatingSecondary,
                                    line: playerDraft.Line,
                                    pn: playerNumber
                            );
            countSaveOrUpdated = countSaveOrUpdated + lo30ContextService.SaveOrUpdateTeamRoster(teamRoster);
          }

        }
      }


      Print("SaveOrUpdateTeamRosters: TeamRosters Count: " + context.TeamRosters.Count() + " SaveOrUpdated:" + countSaveOrUpdated);
      var diffFromLast = DateTime.Now - last;
      Print("SaveOrUpdateTeamRosters: " + diffFromLast.ToString());
    }

    private static void ProcessScoreSheets(int seasonId, bool playoff, int startingGameId, int endingGameId)
    {
      using (var context = new Lo30Context())
      {
        var _lo30ContextService = new Lo30ContextService(context);

        var repo = new Lo30Repository(context, _lo30ContextService);
        ProcessingResult results = new ProcessingResult();

        results = repo.ProcessScoreSheetEntries(startingGameId, endingGameId);
        Print("ProcessScoreSheets.ProcessScoreSheetEntries Modified:" + results.modified + " Time:" + results.time);

        if (string.IsNullOrWhiteSpace(results.error))
        {
          results = repo.ProcessScoreSheetEntryPenalties(startingGameId, endingGameId);
          Print("ProcessScoreSheets.ProcessScoreSheetEntryPenalties Modified:" + results.modified + " Time:" + results.time);
        }

        if (string.IsNullOrWhiteSpace(results.error))
        {
          results = repo.ProcessScoreSheetEntriesIntoGameResults(startingGameId, endingGameId);
          Print("ProcessScoreSheets.ProcessScoreSheetEntriesIntoGameResults Modified:" + results.modified + " Time:" + results.time);
        }

        if (string.IsNullOrWhiteSpace(results.error))
        {
          results = repo.ProcessGameResultsIntoTeamStandings(seasonId, playoff, startingGameId, endingGameId);
          Print("ProcessScoreSheets.ProcessGameResultsIntoTeamStandings Modified:" + results.modified + " Time:" + results.time);
        }

        if (string.IsNullOrWhiteSpace(results.error))
        {
          results = repo.ProcessScoreSheetEntriesIntoPlayerStats(startingGameId, endingGameId);
          Print("ProcessScoreSheets.ProcessScoreSheetEntriesIntoPlayerStats Modified:" + results.modified + " Time:" + results.time);
        }

        if (string.IsNullOrWhiteSpace(results.error))
        {
          results = repo.ProcessPlayerStatsIntoWebStats();
          Print("ProcessScoreSheets.ProcessPlayerStatsIntoWebStats Modified:" + results.modified + " Time:" + results.time);
        }

        if (string.IsNullOrWhiteSpace(results.error))
        {
          Print("ProcessScoreSheets no errors!");
        }
        else
        {
          Print("ProcessScoreSheets Error:" + results.error);
        }
      }
    }
  }
}
