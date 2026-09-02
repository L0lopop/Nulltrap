using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Nulltrap.Core.Roblox;

public sealed class UserGameSettings
{
    public const string FileName = "GlobalBasicSettings_13.xml";
    public const string ItemClass = "UserGameSettings";

    private static readonly XmlWriterSettings AsRobloxWritesIt = new()
    {
        OmitXmlDeclaration = true,
        Indent = true,
        IndentChars = "\t",
        NewLineChars = "\n",
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
    };

    private readonly string _path;

    private XDocument? _document;
    private XElement? _properties;
    private bool _touched;

    public UserGameSettings(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Roblox",
        FileName);

    public bool Loaded => _properties is not null;

    public bool Load()
    {
        _document = null;
        _properties = null;
        _touched = false;

        if (!File.Exists(_path))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            _document = XDocument.Load(stream);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return false;
        }

        _properties = _document.Root
            ?.Elements("Item")
            .FirstOrDefault(item => (string?)item.Attribute("class") == ItemClass)
            ?.Element("Properties");

        return _properties is not null;
    }

    public double? Number(string name)
    {
        string? text = Find(name)?.Value;

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : null;
    }

    public bool? Flag(string name)
    {
        string? text = Find(name)?.Value;

        return text is null ? null : text.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public void SetNumber(string name, double value, int decimals = 0)
    {
        XElement? element = Find(name);

        if (element is null)
        {
            return;
        }

        string text = decimals <= 0
            ? ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
            : Math.Round(value, decimals).ToString("0.#########", CultureInfo.InvariantCulture);

        if (element.Value == text)
        {
            return;
        }

        element.Value = text;
        _touched = true;
    }

    public void SetFlag(string name, bool value)
    {
        XElement? element = Find(name);
        string text = value ? "true" : "false";

        if (element is null || element.Value == text)
        {
            return;
        }

        element.Value = text;
        _touched = true;
    }

    public bool Save()
    {
        if (_document is null || !_touched)
        {
            return false;
        }

        string staging = _path + ".nulltrap";

        try
        {
            using (var stream = new FileStream(staging, FileMode.Create, FileAccess.Write, FileShare.None))
            using (XmlWriter writer = XmlWriter.Create(stream, AsRobloxWritesIt))
            {
                _document.Save(writer);
            }

            File.Move(staging, _path, overwrite: true);
            _touched = false;

            return true;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private XElement? Find(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return _properties?.Elements().FirstOrDefault(element => (string?)element.Attribute("name") == name);
    }
}
