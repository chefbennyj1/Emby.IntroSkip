using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntroSkip;
using IntroSkip.Configuration;
using IntroSkip.Data;
using IntroSkip.TitleSequence;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Serialization;
using SQLitePCL.pretty;

namespace Emby.AutoOrganize.Data
{
    public class SqliteTitleSequenceRepository : BaseSqliteRepository, ITitleSequenceRepository, IDisposable
    {
        private readonly IJsonSerializer _json;
        
        public SqliteTitleSequenceRepository(ILogger logger, IServerApplicationPaths appPaths, IJsonSerializer json) : base(logger)
        {
            _json = json;
            DbFilePath = Path.Combine(appPaths.DataPath, "titlesequence.db");
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
                     "create table if not exists TitleSequenceResults (ResultId INT PRIMARY KEY, TitleSequenceStart TEXT, TitleSequenceEnd TEXT, HasSequence TEXT, Fingerprint TEXT, Duration TEXT, SeriesId INT, SeasonId INT, IndexNumber INT, Confirmed TEXT, Processed TEXT)",                                                                                              
                     "create index if not exists idx_TitleSequenceResults on TitleSequenceResults(ResultId)"
                };

                connection.RunQueries(queries);
                
            }
        }



        public void SaveResult(TitleSequenceResult result, CancellationToken cancellationToken)
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
                        var commandText = "replace into TitleSequenceResults (ResultId, TitleSequenceStart, TitleSequenceEnd, HasSequence, Fingerprint, Duration, SeriesId, SeasonId, IndexNumber, Confirmed, Processed) values (@ResultId, @TitleSequenceStart, @TitleSequenceEnd, @HasSequence, @Fingerprint, @Duration, @SeriesId, @SeasonId, @IndexNumber, @Confirmed, @Processed)";

                        using (var statement = db.PrepareStatement(commandText))
                        {
                            statement.TryBind("@ResultId", result.InternalId);
                            statement.TryBind("@TitleSequenceStart", result.TitleSequenceStart.ToString());
                            statement.TryBind("@TitleSequenceEnd", result.TitleSequenceEnd.ToString());
                            statement.TryBind("@HasSequence", result.HasSequence);
                            statement.TryBind("@Fingerprint", string.Join("|", result.Fingerprint.ToArray()));
                            statement.TryBind("@Duration", result.Duration.ToString());
                            statement.TryBind("@SeriesId", result.SeriesId);
                            statement.TryBind("@SeasonId", result.SeasonId);
                            statement.TryBind("@IndexNumber", result.IndexNumber);
                            statement.TryBind("@Confirmed", result.Confirmed);
                            statement.TryBind("@Processed", result.Processed);
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
                        using (var statement = db.PrepareStatement("delete from TitleSequenceResults where ResultId = @ResultId"))
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
                        var commandText = "delete from TitleSequenceResults";

                        db.Execute(commandText);

                    }, TransactionMode);

                    connection.Vacuum();
                }
            }
        }
              
        public QueryResult<TitleSequenceResult> GetResults(TitleSequenceResultQuery query)
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
                    if(query.SeasonInternalId.HasValue)
                    {
                        commandText = string.Format("SELECT ResultId, TitleSequenceStart, TitleSequenceEnd, HasSequence, Fingerprint, Duration, SeriesId, SeasonId, IndexNumber, Confirmed, Processed from TitleSequenceResults WHERE SeasonId = {0}", query.SeasonInternalId.Value.ToString());
                    } 
                    else
                    {
                        commandText = "SELECT ResultId, TitleSequenceStart, TitleSequenceEnd, HasSequence, Fingerprint, Duration, SeriesId, SeasonId, IndexNumber, Confirmed, Processed from TitleSequenceResults";
                    }   
                       

                    if (query.StartIndex.HasValue && query.StartIndex.Value > 0)
                    {
                        commandText += string.Format(" WHERE ResultId NOT IN (SELECT ResultId FROM TitleSequenceResults ORDER BY SeasonId desc LIMIT {0})",
                            query.StartIndex.Value.ToString(CultureInfo.InvariantCulture));
                    }

                    commandText += " ORDER BY SeasonId desc";

                    if (query.Limit.HasValue)
                    {
                        commandText += " LIMIT " + query.Limit.Value.ToString(CultureInfo.InvariantCulture);
                    }

                    var list = new List<TitleSequenceResult>();

                    using (var statement = connection.PrepareStatement(commandText))
                    {
                        foreach (var row in statement.ExecuteQuery())
                        {
                            list.Add(GetResult(row));
                        }
                    }

                    int count;
                    using (var statement = connection.PrepareStatement("select count (ResultId) from TitleSequenceResults"))
                    {
                        count = statement.ExecuteQuery().First().GetInt(0);
                    }

                    return new QueryResult<TitleSequenceResult>()
                    {
                        Items = list.ToArray(),
                        TotalRecordCount = count
                    };
                }
            }
        }
        public TitleSequenceResult GetResult(long id)
        {
            string stringId = id.ToString();
            if (string.IsNullOrEmpty(stringId))
            {
                throw new ArgumentNullException("id");
            }

            using (WriteLock.Read())
            {
                using (var connection = CreateConnection(true))
                {
                    using (var statement = connection.PrepareStatement("select ResultId, TitleSequenceStart, TitleSequenceEnd, HasSequence, Fingerprint, Duration, SeriesId, SeasonId, IndexNumber, Confirmed, Processed from TitleSequenceResults where ResultId=@ResultId"))
                    {
                        statement.TryBind("@ResultId", Convert.ToInt64(id));

                        foreach (var row in statement.ExecuteQuery())
                        {
                            return GetResult(row);
                        }
                    }

                    return null;
                }
            }
        }
        public TitleSequenceResult GetResult(IResultSet reader)
        {
            var index = 0;

            var result = new TitleSequenceResult
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
                result.HasSequence = reader.GetBoolean(index);
            }

            index++;
            if (!reader.IsDBNull(index))
            {
                result.Fingerprint = ToUintList(reader.GetString(index).Split('|'));
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
            

            return result;
        }

       private List<uint> ToUintList(Array a)
       {
            var list = new List<uint>();
            foreach(var item in a)
            {
                list.Add(Convert.ToUInt32(item));
            }
            return list;
       }

    }
}
