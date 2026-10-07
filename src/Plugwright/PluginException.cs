namespace Plugwright;

/// <summary>
/// Thrown when a plugin cannot be loaded, configured or used.
/// </summary>
/// <remarks>
/// The message names the plugin and the cause. The original exception, if any, is available as
/// <see cref="Exception.InnerException"/>.
/// </remarks>
public sealed class PluginException : Exception
{
    /// <summary>
    /// Creates a plugin exception.
    /// </summary>
    public PluginException()
    {
    }

    /// <summary>
    /// Creates a plugin exception with a message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PluginException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a plugin exception with a message and the exception that caused it.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public PluginException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates a plugin exception for a specific plugin.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="pluginId">The ID of the plugin that caused the error.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public PluginException(string message, string? pluginId, Exception? innerException)
        : base(message, innerException)
    {
        PluginId = pluginId;
    }

    /// <summary>
    /// Gets the ID of the plugin that caused the error, if known.
    /// </summary>
    public string? PluginId { get; }
}
