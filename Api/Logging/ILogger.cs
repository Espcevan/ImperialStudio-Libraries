﻿using System;

namespace ImperialStudio.Core.Api.Logging
{
    /// <summary>
    ///     A provider for logging.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        ///     Logs a message.
        /// </summary>
        /// <param name="level">Thel log level.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The related exception if one exists.</param>
        /// <param name="arguments">The message arguments. See <see cref="string.Format(string, object[])" /></param>
        void Log(string message, LogLevel level = LogLevel.Information, Exception exception = null,
            params object[] arguments);

        /// <summary>
        ///     Checks if a log level is enabled.
        /// </summary>
        /// <param name="level">The log level to check.</param>
        /// <returns><b>true</b> if the log level is enabled; otherwise, <b>false</b>.</returns>
        bool IsEnabled(LogLevel level);

        void SetEnabled(LogLevel level, bool enabled);
    }
}