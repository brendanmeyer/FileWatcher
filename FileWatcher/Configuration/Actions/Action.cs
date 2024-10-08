﻿using System.Xml.Serialization;
using TE.FileWatcher.Logging;
using TEFS = TE.FileWatcher.FileSystem;

namespace TE.FileWatcher.Configuration.Actions
{
    /// <summary>
    /// The Action to perform during a watch event.
    /// </summary>
    public class Action : RunnableBase
    {
        /// <summary>
        /// The type of action to perform.
        /// </summary>
        [Serializable]
        public enum ActionType
        {
            /// <summary>
            /// Copy a file.
            /// </summary>
            Copy,
            /// <summary>
            /// Move a file.
            /// </summary>
            Move,
            /// <summary>
            /// Delete a file.
            /// </summary>
            Delete
        }

        /// <summary>
        /// Gets or sets the type of action to perform.
        /// </summary>
        [XmlElement("type")]
        public ActionType Type { get; set; }

        /// <summary>
        /// Gets or sets the source of the action.
        /// </summary>
        [XmlElement("source")]
        public string Source { get; set; } = PLACEHOLDER_FULLPATH;

        /// <summary>
        /// Gets or sets the triggers of the action.
        /// </summary>
        [XmlElement("triggers")]
        public Triggers Triggers { get; set; } = new Triggers();

        /// <summary>
        /// Gets or sets the destination of the action.
        /// </summary>
        [XmlElement("destination")]
        public string? Destination { get; set; }

        /// <summary>
        /// Gets or sets the verify flag.
        /// </summary>
        [XmlElement(ElementName = "verify", DataType = "boolean")]
        public bool Verify { get; set; }

        /// <summary>
        /// Gets or sets the keep timestamps flag.
        /// </summary>
        [XmlElement(ElementName = "keepTimestamps", DataType = "boolean")]
        public bool KeepTimestamps { get; set; }

        /// <summary>
        /// Runs the action.
        /// </summary>
        /// <param name="watchPath">
        /// The watch path.
        /// </param>
        /// <param name="fullPath">
        /// The full path to the changed file or folder.
        /// </param>
        /// <param name="trigger">
        /// The trigger for the action.
        /// </param>
        public override void Run(string watchPath, string fullPath, TriggerType trigger)
        {
            if (string.IsNullOrWhiteSpace(watchPath) || string.IsNullOrWhiteSpace(fullPath))
            {
                return;
            }

            if (Triggers == null || Triggers.TriggerList == null)
            {
                return;
            }

            if (Triggers.TriggerList.Count <= 0 || !Triggers.Current.HasFlag(trigger))
            {
                return;
            }

            string? source = GetSource(watchPath, fullPath);
            string? destination = GetDestination(watchPath, fullPath);

            if (string.IsNullOrWhiteSpace(source))
            {
                Logger.WriteLine(
                    $"The source file could not be determined. Watch path: {watchPath}, changed: {fullPath}.",
                    LogLevel.ERROR);
                return;
            }

            if (!TEFS.File.IsValid(source))
            {
                Logger.WriteLine(
                    $"The file '{source}' could not be {GetActionString()} because the path was not valid, the file doesn't exists, or it was in use.",
                    LogLevel.ERROR);
                return;
            }

            try
            {
                switch (Type)
                {
                    case ActionType.Copy:
                        if (string.IsNullOrWhiteSpace(destination))
                        {
                            Logger.WriteLine(
                                $"The file '{source}' could not be copied because the destination file could not be determined. Destination in config file: {Destination}.",
                                LogLevel.ERROR);
                            return;
                        }

                        TEFS.File.Copy(source, destination, Verify, KeepTimestamps);
                        Logger.WriteLine($"Copied {source} to {destination}. Verify: {Verify}. Keep timestamps: {KeepTimestamps}.");
                        break;

                    case ActionType.Move:
                        if (string.IsNullOrWhiteSpace(destination))
                        {
                            Logger.WriteLine(
                                $"The file '{source}' could not be moved because the destination file could not be determined. Destination in config file: {Destination}.",
                                LogLevel.ERROR);
                            return;
                        }

                        TEFS.File.Move(source, destination, Verify, KeepTimestamps);
                        Logger.WriteLine($"Moved {source} to {destination}. Verify: {Verify}. Keep timestamps: {KeepTimestamps}.");
                        break;

                    case ActionType.Delete:
                        TEFS.File.Delete(source);
                        Logger.WriteLine($"Deleted {source}.");
                        break;
                }
            }
            catch (Exception ex)
            {
                string message = (ex.InnerException == null) ? ex.Message : ex.InnerException.Message;
                Logger.WriteLine(
                    $"Could not {Type.ToString().ToLower()} file '{source}.' Reason: {message}",
                    LogLevel.ERROR);
                return;
            }
        }

        /// <summary>
        /// Gets the string value that represents the action type.
        /// </summary>
        /// <returns>
        /// A string value for the action type, otherwise <c>null</c>.
        /// </returns>
        private string? GetActionString()
        {
            return Type switch
            {
                ActionType.Copy => "copied",
                ActionType.Move => "moved",
                ActionType.Delete => "deleted",
                _ => null
            };
        }

        /// <summary>
        /// Gets the destination value by replacing any placeholders with the
        /// actual string values.
        /// </summary>
        /// <param name="watchPath">
        /// The watch path.
        /// </param>
        /// <param name="fullPath">
        /// The full path of the changed file.
        /// </param>
        /// <returns>
        /// The destination string value.
        /// </returns>
        private string? GetDestination(string watchPath, string fullPath)
        {
            if (string.IsNullOrWhiteSpace(Destination))
            {
                return null;
            }

            string? destination = ReplacePlaceholders(Destination, watchPath, fullPath);
            if (!string.IsNullOrWhiteSpace(destination))
            {
                destination = ReplaceDatePlaceholders(destination, watchPath, fullPath);
            }
            return destination;
        }

        /// <summary>
        /// Gets the source value by replacing any placeholders with the actual
        /// string values.
        /// </summary>
        /// <param name="watchPath">
        /// The watch path.
        /// </param>
        /// <param name="fullPath">
        /// The full path of the changed file.
        /// </param>
        /// <returns>
        /// The source string value.
        /// </returns>
        private string? GetSource(string watchPath, string fullPath)
        {
            if (string.IsNullOrWhiteSpace(Source))
            {
                return null;
            }
           
            string? source = ReplacePlaceholders(Source, watchPath, fullPath);
            if (!string.IsNullOrWhiteSpace(source))
            {
                source = ReplaceDatePlaceholders(source, watchPath, fullPath);
            }

            return source;
        }


    }
}
