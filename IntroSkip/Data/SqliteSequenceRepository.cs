using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using IntroSkip.Sequence;
using MediaBrowser.Controller;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Serialization;
using SQLitePCL.pretty;

namespace IntroSkip.Data
{
    public class SqliteSequenceRepository : BaseSqliteRepository, ISequenceRepository
    {
        private readonly IJsonSerializer _json;
        private IFileSystem FileSystem { get; set; }
        //private ILogger Logger { get; set; }
        private IServerApplicationPaths AppPaths { get; set; }
        public SqliteSequenceRepository(ILogger logger, IServerApplicationPaths appPaths, IJsonSerializer json, IFileSystem fileSystem) : base(logger)
        {
            _json = json;
            FileSystem = fileSystem;
            AppPaths = appPaths;
            DbFilePath = Path.Combine(appPaths.DataPath, "titlesequence.db");
            //Logger = logger;
        }

        public void Backup()
        {
            var backups = new List<FileSystemMetadata>();
            try
            {
                backups = FileSystem.GetFiles(AppPaths.DataPath).Where(f => f.Name.Contains("titlesequence_"))
                    .ToList(); //our backup files
            }
            catch { }

            if (backups.Any()) //Only remove files if we have more then 2 of them
            {
                if (backups.Count > 2)
                {
                    foreach (var file in backups)
                    {
                        var fileBackupDate =
                            DateTime.Parse(file.Name.Split('_')[1]
                                .Split('.')[0]); //The date the file backup happened is on the name
                        if (fileBackupDate >= DateTime.Now.AddDays(-3))
                            continue; // only clean up files if the backup is older then 3 days
                        try
                        {
                            FileSystem.DeleteFile(file.FullName); //Get rid of old backups
                        }
                        catch (Exception ex)
                        {
                            //Logger.Warn(ex.Message);
                        }
                    }
                }
            }

            try
            {
                FileSystem.CopyFile(DbFilePath, Path.Combine(AppPaths.DataPath, $"titlesequence_{DateTime.Now:yy-MM-dd}.db"), true); //Create the new backup
                //Logger.Debug("Sequence Database backup complete.");
            }
            catch (Exception ex)
            {
                //Logger.Warn(ex.Message);
            }
            
        }
        /// <summary>
        /// Opens the connection to the database
        /// </summary>
        /// <returns>Task.</returns>
        public void Initialize()
        {
            using (var connection = CreateConnection())
            {
                RunDefaultInitialization(connection);

                string[] queries =
                {
                     "create table if not exists SequenceResults (ResultId INT PRIMARY KEY, TitleSequenceStart TEXT, TitleSequenceEnd TEXT, CreditSequenceStart TEXT, CreditSequenceEnd TEXT, HasTitleSequence TEXT, HasCreditSequence TEXT, TitleSequenceFingerprint TEXT, CreditSequenceFingerprint TEXT, Duration TEXT, SeriesId INT, SeasonId INT, IndexNumber INT, Confirmed TEXT, Processed TEXT, HasRecap TEXT)",
                     "create index if not exists idx_SequenceResults on SequenceResults(ResultId)"
                };

                connection.RunQueries(queries);

            }
        }


        public void SaveResult(SequenceResult result, CancellationToken cancellationToken)
        {
            if (result == null)
            {
                throw new ArgumentNullException("result");
            }

            cancellationToken.ThrowIfCancellationRequested();

            using (WriteLock.Write())
            {
                using (var connection = CreateConnection())
                {
                    connection.RunInTransaction(db =>
                    {
                        var commandText = "replace into SequenceResults (ResultId, TitleSequenceStart, TitleSequenceEnd, CreditSequenceStart, CreditSequenceEnd, HasTitleSequence, HasCreditSequence, TitleSequenceFingerprint, CreditSequenceFingerprint, Duration, SeriesId, SeasonId, IndexNumber, Confirmed, Processed, HasRecap) values (@ResultId, @TitleSequenceStart, @TitleSequenceEnd, @CreditSequenceStart, @CreditSequenceEnd, @HasTitleSequence, @HasCreditSequence, @TitleSequenceFingerprint, @CreditSequenceFingerprint, @Duration, @SeriesId, @SeasonId, @IndexNumber, @Confirmed, @Processed, @HasRecap)";

                        using (var statement = db.PrepareStatement(commandText))
                        {
                            statement.TryBind("@ResultId", result.InternalId);
                            statement.TryBind("@TitleSequenceStart", result.TitleSequenceStart.ToString());
                            statement.TryBind("@TitleSequenceEnd", result.TitleSequenceEnd.ToString());
                            statement.TryBind("@CreditSequenceStart", result.CreditSequenceStart.ToString());
                            statement.TryBind("@CreditSequenceEnd", result.CreditSequenceEnd.ToString());
                            statement.TryBind("@HasTitleSequence", result.HasTitleSequence);
                            statement.TryBind("@HasCreditSequence", result.HasCreditSequence);
                            statement.TryBind("@TitleSequenceFingerprint", string.Join("|", result.TitleSequenceFingerprint.ToArray()));
                            statement.TryBind("@CreditSequenceFingerprint", string.Join("|", result.CreditSequenceFingerprint.ToArray()));
                            statement.TryBind("@Duration", result.Duration.ToString());
                            statement.TryBind("@SeriesId", result.SeriesId);
                            statement.TryBind("@SeasonId", result.SeasonId);
                            statement.TryBind("@IndexNumber", result.IndexNumber);
                            statement.TryBind("@Confirmed", result.Confirmed);
                            statement.TryBind("@Processed", result.Processed);
                            statement.TryBind("@HasRecap", result.HasRecap);
                            statement.MoveNext();
                        }
                    }, TransactionMode);
                }
            }
        }

        public void Vacuum()
        {
            using (WriteLock.Write())
            {
                using (var connection = CreateConnection())
                {
                    //connection.RunInTransaction(db =>
                    //{
                    connection.Vacuum();

                    //}, TransactionMode);

                }
            }
        }
        public void Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException("id");
            }

            using (WriteLock.Write())
            {
                using (var connection = CreateConnection())
                {
                    connection.RunInTransaction(db =>
                    {
                        using (var statement = db.PrepareStatement("delete from SequenceResults where ResultId = @ResultId"))
                        {
                            statement.TryBind("@ResultId", Convert.ToInt64(id));
                            statement.MoveNext();
                        }
                    }, TransactionMode);

                }
            }
        }

        public void DeleteAll()
        {
            using (WriteLock.Write())
            {
                using (var connection = CreateConnection())
                {
                    connection.RunInTransaction(db =>
                    {
                        var commandText = "delete from SequenceResults";

                        db.Execute(commandText);

                    }, TransactionMode);

                    connection.Vacuum();
                }
            }
        }


        //BaseTitleSequence
        public QueryResult<BaseSequence> GetBaseTitleSequenceResults(SequenceResultQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            using (WriteLock.Read())
            {
                using (var connection = CreateConnection(true))
                {
                    var commandText = string.Empty;
                    if (query.SeasonInternalId.HasValue)
                    {
                        commandText = string.Format("SELECT ResultId, TitleSequenceStart, TitleSequenceEnd, CreditSequenceStart, CreditSequenceEnd, HasTitleSequence, HasCreditSequence, SeriesId, SeasonId, IndexNumber, Confirmed, Processed, HasRecap from SequenceResults WHERE SeasonId = {0}", query.SeasonInternalId.Value.ToString());
                    }
                    else
                    {
                        commandText = "SELECT ResultId, TitleSequenceStart, TitleSequenceEnd, CreditSequenceStart, CreditSequenceEnd, HasTitleSequence, HasCreditSequence, SeriesId, SeasonId, IndexNumber, Confirmed, Processed, HasRecap from SequenceResults";
                    }


                    if (query.StartIndex.HasValue && query.StartIndex.Value > 0)
                    {
                        commandText += string.Format(" WHERE ResultId NOT IN (SELECT ResultId FROM SequenceResults ORDER BY IndexNumber asc LIMIT {0})",
                            query.StartIndex.Value.ToString(CultureInfo.InvariantCulture));
                    }

                    commandText += " ORDER BY IndexNumber asc";

                    if (query.Limit.HasValue)
                    {
                        commandText += " LIMIT " + query.Limit.Value.ToString(CultureInfo.InvariantCulture);
                    }

                    var list = new List<BaseSequence>();

                    using (var statement = connection.PrepareStatement(commandText))
                    {
                        foreach (var row in statement.ExecuteQuery())
                        {
                            list.Add(GetBaseSequenceResult(row));
                        }
                    }

                    int count;
                    using (var statement = connection.PrepareStatement("select count (ResultId) from SequenceResults"))
                    {
                        count = statement.ExecuteQuery().First().GetInt(0);
                    }

                    return new QueryResult<BaseSequence>()
                    {
                        Items = list.ToArray(),
                        TotalRecordCount = count
                    };
                }
            }
        }
        public BaseSequence GetBaseTitleSequence(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException("id");
            }

            using (WriteLock.Read())
            {
                using (var connection = CreateConnection(true))
                {
                    using (var statement = connection.PrepareStatement("select ResultId, TitleSequenceStart, TitleSequenceEnd, CreditSequenceStart, CreditSequenceEnd, HasTitleSequence, HasCreditSequence, SeriesId, SeasonId, IndexNumber, Confirmed, Processed, HasRecap from SequenceResults where ResultId=@ResultId"))
                    {
                        statement.TryBind("@ResultId", id);

                        foreach (var row in statement.ExecuteQuery())
                        {
                            return GetBaseSequenceResult(row);
                        }
                    }

                    return null;
                }
            }

        }
        public BaseSequence GetBaseSequenceResult(IResultSet reader)
        {
            var index = 0;

            var result = new BaseSequence
            {
                InternalId = reader.GetInt64(index)
            };

            index++;
            if (!reader.IsDBNull(index))
            {
                result.TitleSequenceStart = TimeSpan.Parse(reader.GetString(index));
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.TitleSequenceEnd = TimeSpan.Parse(reader.GetString(index));
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.CreditSequenceStart = TimeSpan.Parse(reader.GetString(index));
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.CreditSequenceEnd = TimeSpan.Parse(reader.GetString(index));
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.HasTitleSequence = reader.GetBoolean(index);
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.HasCreditSequence = reader.GetBoolean(index);
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.SeriesId = reader.GetInt64(index);
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.SeasonId = reader.GetInt64(index);
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.IndexNumber = reader.GetInt(index);
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.Confirmed = reader.GetBoolean(index);
            }
            index++;
            if (!reader.IsDBNull(index))
            {
                result.Processed = reader.GetBoolean(index);
            }
            index++;
            if (!reader.IsDBNull(index))
            {
                result.HasRecap = reader.GetBoolean(index);
            }


            return result;
        }

        //TitleSequenceResult - Full Result including Fingerprint and duration
        public QueryResult<SequenceResult> GetResults(SequenceResultQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            using (WriteLock.Read())
            {
                using (var connection = CreateConnection(true))
                {
                    var commandText = string.Empty;
                    if (query.SeasonInternalId.HasValue)
                    {
                        commandText = string.Format("SELECT ResultId, TitleSequenceStart, TitleSequenceEnd, CreditSequenceStart, CreditSequenceEnd, HasTitleSequence, HasCreditSequence, TitleSequenceFingerprint, CreditSequenceFingerprint, Duration, SeriesId, SeasonId, IndexNumber, Confirmed, Processed, HasRecap from SequenceResults WHERE SeasonId = {0}", query.SeasonInternalId.Value.ToString());
                    }
                    else
                    {
                        commandText = "SELECT ResultId, TitleSequenceStart, TitleSequenceEnd, CreditSequenceStart, CreditSequenceEnd, HasTitleSequence, HasCreditSequence, TitleSequenceFingerprint, CreditSequenceFingerprint, Duration, SeriesId, SeasonId, IndexNumber, Confirmed, Processed, HasRecap from SequenceResults";
                    }


                    if (query.StartIndex.HasValue && query.StartIndex.Value > 0)
                    {
                        commandText += string.Format(" WHERE ResultId NOT IN (SELECT ResultId FROM SequenceResults ORDER BY SeasonId desc LIMIT {0})",
                            query.StartIndex.Value.ToString(CultureInfo.InvariantCulture));
                    }

                    commandText += " ORDER BY SeasonId desc";

                    if (query.Limit.HasValue)
                    {
                        commandText += " LIMIT " + query.Limit.Value.ToString(CultureInfo.InvariantCulture);
                    }

                    var list = new List<SequenceResult>();

                    using (var statement = connection.PrepareStatement(commandText))
                    {
                        foreach (var row in statement.ExecuteQuery())
                        {
                            list.Add(GetResult(row));
                        }
                    }

                    int count;
                    using (var statement = connection.PrepareStatement("select count (ResultId) from SequenceResults"))
                    {
                        count = statement.ExecuteQuery().First().GetInt(0);
                    }

                    return new QueryResult<SequenceResult>()
                    {
                        Items = list.ToArray(),
                        TotalRecordCount = count
                    };
                }
            }
        }
        public SequenceResult GetResult(string id)
        {

            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException("id");
            }

            using (WriteLock.Read())
            {
                using (var connection = CreateConnection(true))
                {
                    using (var statement = connection.PrepareStatement("select ResultId, TitleSequenceStart, TitleSequenceEnd, CreditSequenceStart, CreditSequenceEnd, HasTitleSequence, HasCreditSequence, TitleSequenceFingerprint, CreditSequenceFingerprint, Duration, SeriesId, SeasonId, IndexNumber, Confirmed, Processed, HasRecap from SequenceResults where ResultId=@ResultId"))
                    {
                        statement.TryBind("@ResultId", id);

                        foreach (var row in statement.ExecuteQuery())
                        {
                            return GetResult(row);
                        }
                    }

                    return null;
                }
            }
        }
        public SequenceResult GetResult(IResultSet reader)
        {
            var index = 0;

            var result = new SequenceResult
            {
                InternalId = reader.GetInt64(index)
            };

            index++;
            if (!reader.IsDBNull(index))
            {
                result.TitleSequenceStart = TimeSpan.Parse(reader.GetString(index));
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.TitleSequenceEnd = TimeSpan.Parse(reader.GetString(index));
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.CreditSequenceStart = TimeSpan.Parse(reader.GetString(index));
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.CreditSequenceEnd = TimeSpan.Parse(reader.GetString(index));
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.HasTitleSequence = reader.GetBoolean(index);
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.HasCreditSequence = reader.GetBoolean(index);
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.TitleSequenceFingerprint = ToUintList(reader.GetString(index).Split('|'));
            }
            index++;
            if (!reader.IsDBNull(index))
            {
                result.CreditSequenceFingerprint = ToUintList(reader.GetString(index).Split('|'));
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.Duration = reader.GetDouble(index);
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.SeriesId = reader.GetInt64(index);
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.SeasonId = reader.GetInt64(index);
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.IndexNumber = reader.GetInt(index);
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.Confirmed = reader.GetBoolean(index);

            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.Processed = reader.GetBoolean(index);
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.HasRecap = reader.GetBoolean(index);
            }

            return result;
        }

        private List<uint> ToUintList(Array a)
        {
            var list = new List<uint>();
            foreach (var item in a)
            {
                list.Add(Convert.ToUInt32(item));
            }
            return list;
        }

    }
}