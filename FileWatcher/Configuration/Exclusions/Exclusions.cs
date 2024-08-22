using TE.FileWatcher.Configuration.Data;

namespace TE.FileWatcher.Configuration.Exclusions
{
    /// <summary>
    /// An exclusions node in the XML file.
    /// </summary>
    public class Exclusions : MatchBase
    {
        /// <summary>
        /// Returns the flag indicating if the change is to be ignored.
        /// </summary>
        /// <param name="watchPath">
        /// The path associated with the watch.
        /// </param>
        /// <param name="name">
        /// The name of the file or folder.
        /// </param>
        /// <<param name="fullPath">
        /// The full path to the changed file or folder.
        /// </param>
        /// <returns>
        /// True if the change is to be ignored, otherwise false.
        /// </returns>
        /// <exception cref="FileWatcherException">
        /// Thrown when there is a problem with the path.
        /// </exception>
        public bool Exclude(ChangeInfo change, string watchPath, string name, string fullPath)
        {
            TriggerTypeName = $"{change.Trigger.ToString()} ";
            FilterTypeName = "Exclude";
            return IsMatchFound(watchPath, name, fullPath);
        }
    }
}
