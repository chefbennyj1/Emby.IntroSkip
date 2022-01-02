using System;
using MediaBrowser.Model.Querying;
using System.Threading;
using IntroSkip.Sequence;

namespace IntroSkip.Data
{
    public interface ISequenceRepository : IDisposable
    {
        void Backup();
        /// <summary>
        /// Saves the result.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        void SaveResult(SequenceResult result, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes the specified identifier.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns>Task.</returns>
        void Delete(string id);

        /// <summary>
        /// Get TitleSequence Base Items
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        QueryResult<BaseSequence> GetBaseTitleSequenceResults(SequenceResultQuery query);

        /// <summary>
        /// Get title sequence base item
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        BaseSequence GetBaseTitleSequence(string id);

        void Vacuum();

        /// <summary>
        /// Gets the result.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns>TitleSequenceResult.</returns>
        SequenceResult GetResult(string id);
        /// <summary>
        /// Gets the results.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>IEnumerable{TitleSequenceResult}.</returns>
        QueryResult<SequenceResult> GetResults(SequenceResultQuery query);

        /// <summary>
        /// Deletes all.
        /// </summary>
        /// <returns>Task.</returns>
        void DeleteAll();

        //void CreateColumn(string name, string type);
        //bool ColumnExists(string columnName);

        //void CreateColumn(string columnName, string type);

    }
}
