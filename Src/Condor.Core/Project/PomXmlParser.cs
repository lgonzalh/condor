using System.Xml.Linq;

namespace Condor.Core.Project;

public static class PomXmlParser
{
    public static ManifestContent Parse(string text)
    {
        var content = new ManifestContent();
        try
        {
            var xml = XDocument.Parse(text);
            var root = xml.Root;
            if (root is null || root.Name.LocalName != "project")
            {
                content.ParseError = true;
                return content;
            }

            content.Name = ElementValueByLocalName(root, "artifactId");
            content.Version = ElementValueByLocalName(root, "version");

            var dependencies = root.Elements().FirstOrDefault(e => e.Name.LocalName == "dependencies");
            if (dependencies is not null)
            {
                foreach (var dependency in dependencies.Elements())
                {
                    if (dependency.Name.LocalName != "dependency")
                    {
                        continue;
                    }

                    var artifactId = ElementValueByLocalName(dependency, "artifactId");
                    if (!string.IsNullOrWhiteSpace(artifactId))
                    {
                        content.Dependencies.Add(artifactId);
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

    private static string? ElementValueByLocalName(XElement element, string localName)
    {
        var child = element.Elements().FirstOrDefault(e => e.Name.LocalName == localName);
        var value = child?.Value?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}