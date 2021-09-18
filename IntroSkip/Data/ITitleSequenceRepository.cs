using IntroSkip.TitleSequence;
using MediaBrowser.Model.Querying;
using System.Threading;

namespace IntroSkip.Data
{
    public interface ITitleSequenceRepository
    {
        /// <summary>
        /// Saves the result.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        void SaveResult(TitleSequenceResult result, CancellationToken cancellationToken);

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
        QueryResult<BaseTitleSequence> GetBaseTitleSequenceResults(TitleSequenceResultQuery query);

        /// <summary>
        /// Get title sequence base item
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        BaseTitleSequence GetBaseTitleSequence(string id);

        void Vacuum();

        /// <summary>
        /// Gets the result.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns>TitleSequenceResult.</returns>
        TitleSequenceResult GetResult(string id);
        /// <summary>
        /// Gets the results.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>IEnumerable{TitleSequenceResult}.</returns>
        QueryResult<TitleSequenceResult> GetResults(TitleSequenceResultQuery query);

        /// <summary>
        /// Deletes all.
        /// </summary>
        /// <returns>Task.</returns>
        void DeleteAll();

        //void CreateColumn(string name, string type);



    }
}
