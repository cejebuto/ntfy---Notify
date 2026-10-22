using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Notify.Web.Services;

/// <summary>Holds the ntfy topic currently in use. The topic acts as a password, so it is random by default.</summary>
public sealed class TopicService
{
    public static readonly Regex TopicRegex = new("^[-_A-Za-z0-9]{1,64}$", RegexOptions.Compiled);

    private const string Prefix = "notify-";
    private const int SuffixLength = 24;
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    private readonly object _lock = new();
    private string _current = Generate();

    public string Current
    {
        get { lock (_lock) return _current; }
    }

    public static string Generate()
    {
        var chars = new char[SuffixLength];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        return Prefix + new string(chars);
    }

    public string Regenerate()
    {
        var next = Generate();
        lock (_lock) _current = next;
        return next;
    }

    public bool TrySet(string? topic, out string error)
    {
        var candidate = topic?.Trim();
        if (string.IsNullOrEmpty(candidate))
        {
            error = "Topic is required";
            return false;
        }
        if (!TopicRegex.IsMatch(candidate))
        {
            error = "Topic may only contain letters, numbers, dashes and underscores (max 64 chars)";
            return false;
        }

        lock (_lock) _current = candidate;
        error = string.Empty;
        return true;
    }
}
