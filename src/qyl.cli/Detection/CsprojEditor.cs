using System.Xml.Linq;

namespace qyl.cli.Detection;

/// <summary>
/// Safe csproj XML manipulation — reads and modifies PackageReference elements.
/// </summary>
public sealed class CsprojEditor
{
    private readonly string _path;
    private readonly XDocument _doc;

    public CsprojEditor(string path)
    {
        _path = path;
        _doc = XDocument.Load(path, LoadOptions.PreserveWhitespace);
    }

    /// <summary>
    /// Checks if a PackageReference or ProjectReference with the given name exists.
    /// </summary>
    public bool HasReference(string packageName)
    {
        return _doc.Descendants("PackageReference")
                   .Any(e => string.Equals(e.Attribute("Include")?.Value, packageName, StringComparison.OrdinalIgnoreCase))
               || _doc.Descendants("ProjectReference")
                      .Any(e => (e.Attribute("Include")?.Value ?? "").Contains(packageName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Adds a PackageReference element to the csproj. Idempotent — does nothing if already present.
    /// </summary>
    public void AddPackageReference(string packageName)
    {
        if (HasReference(packageName))
        {
            return;
        }

        // Find an existing ItemGroup with PackageReference elements, or create one
        var itemGroup = _doc.Descendants("ItemGroup")
            .FirstOrDefault(static ig => ig.Elements("PackageReference").Any());

        if (itemGroup is null)
        {
            itemGroup = new XElement("ItemGroup");
            var root = _doc.Root ?? throw new InvalidOperationException("csproj has no root element");
            root.Add(itemGroup);
        }

        itemGroup.Add(new XElement("PackageReference",
            new XAttribute("Include", packageName)));

        _doc.Save(_path);
    }
}
