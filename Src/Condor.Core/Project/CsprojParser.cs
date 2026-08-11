using System.Xml.Linq;

namespace Condor.Core.Project;

public static class CsprojParser
{
    public static ManifestContent Parse(string text)
    {
        var content = new ManifestContent();
        try
        {
            var xml = XDocument.Parse(text);
            var root = xml.Root;
            if (root is null)
            {
                content.ParseError = true;
                return content;
            }

            content.Sdk = root.Attribute("Sdk")?.Value;
            content.UseWpf = HasElementValue(root, "UseWPF", "true");
            content.UseWindowsForms = HasElementValue(root, "UseWindowsForms", "true");

            foreach (var element in root.Descendants())
            {
                if (element.Name.LocalName == "PackageReference" || element.Name.LocalName == "ProjectReference")
                {
                    var include = element.Attribute("Include")?.Value;
                    if (!string.IsNullOrWhiteSpace(include))
                    {
                        content.Dependencies.Add(include);
                    }
                }
            }

            content.CapAndSortDependencies();
        }
        catch (System.Xml.XmlException)
        {
            content.ParseError = true;
        }

        return content;
    }

    private static bool HasElementValue(XElement root, string localName, string expectedValue)
    {
        var element = root.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);
        return element is not null && string.Equals(element.Value.Trim(), expectedValue, StringComparison.OrdinalIgnoreCase);
    }
}