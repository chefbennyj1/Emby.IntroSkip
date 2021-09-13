using IntroSkip.Configuration;
using IntroSkip.TitleSequence;
using MediaBrowser.Model.Querying;
using System.Threading;
using System.Threading.Tasks;

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
        /// Gets the result.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns>TitleSequenceResult.</returns>
        TitleSequenceResult GetResult(string id);

        BaseTitleSequence GetBaseTitleSequence(string id);

        void Vacuum();

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
