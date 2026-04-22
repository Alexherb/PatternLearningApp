using System;
using System.Linq;
using System.Text;

namespace PatternLearningApp
{
    /// <summary>
    /// Einfacher Builder für XPath 1.0-Ausdrücke.
    /// Unterstützt absolute/relative Startpräfixe ("//", ".//", "/", "./") sowie
    /// das Hinzufügen von Child/Descendant-Segmenten und Prädikaten.
    /// Fluent-API.
    /// </summary>
    public class XPathBuilder
    {
        private readonly StringBuilder _sb = new();

        private XPathBuilder(string prefix, string? initialSegment = null)
        {
            _sb.Append(prefix ?? string.Empty);
            if (!string.IsNullOrEmpty(initialSegment))
            {
                // initialSegment is expected to be a tag name or segment that should follow the prefix directly
                _sb.Append(initialSegment);
            }
        }

        // Factory-Methoden für Startpräfixe
        public static XPathBuilder RootDescendant() => new XPathBuilder("//");
        public static XPathBuilder RootDescendant(string tagName)
        {
            EnsureName(tagName);
            return new XPathBuilder("//", tagName);
        }

        public static XPathBuilder RelativeDescendant() => new XPathBuilder(".//");
        public static XPathBuilder RelativeDescendant(string tagName)
        {
            EnsureName(tagName);
            return new XPathBuilder(".//", tagName);
        }

        public static XPathBuilder RootChild() => new XPathBuilder("/");
        public static XPathBuilder RootChild(string tagName)
        {
            EnsureName(tagName);
            return new XPathBuilder("/", tagName);
        }

        public static XPathBuilder RelativeChild() => new XPathBuilder("./");
        public static XPathBuilder RelativeChild(string tagName)
        {
            EnsureName(tagName);
            return new XPathBuilder("./", tagName);
        }

        public static XPathBuilder CustomPrefix(string prefix) => new XPathBuilder(prefix ?? string.Empty);
        public static XPathBuilder CustomPrefix(string prefix, string tagName)
        {
            EnsureName(tagName);
            return new XPathBuilder(prefix ?? string.Empty, tagName);
        }

        // Fügt ein direktes Child mit "/name" hinzu
        public XPathBuilder Child(string name)
        {
            EnsureName(name);
            _sb.Append('/').Append(name);
            return this;
        }

        // Fügt ein direktes Child mit "./name" hinzu (nützlich wenn der Ausdruck relativ zum Kontext sein soll)
        public XPathBuilder CurrentChild(string name)
        {
            EnsureName(name);
            _sb.Append("./").Append(name);
            return this;
        }

        // Fügt ein Descendant mit "//name" hinzu
        public XPathBuilder Descendant(string name)
        {
            EnsureName(name);
            _sb.Append("//").Append(name);
            return this;
        }

        // Fügt ein descendant relativ zum aktuellen Kontext mit ".//name" hinzu
        public XPathBuilder CurrentDescendant(string name)
        {
            EnsureName(name);
            _sb.Append(".//").Append(name);
            return this;
        }

        // Platzhalter für beliebiges Element
        public XPathBuilder Any()
        {
            _sb.Append('*');
            return this;
        }

        // Fügt ein Prädikat an das aktuellste Segment an
        public XPathBuilder Predicate(string xpathPredicate)
        {
            if (string.IsNullOrEmpty(xpathPredicate)) throw new ArgumentException("Predicate darf nicht leer sein", nameof(xpathPredicate));
            _sb.Append('[').Append(xpathPredicate).Append(']');
            return this;
        }

        // Fügt ein Attributvergleichs-Prädikat hinzu: @name = 'value'
        public XPathBuilder AttributeEquals(string name, string value)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Attributname darf nicht leer sein", nameof(name));
            _sb.Append('[').Append('@').Append(name).Append("=").Append(EscapeForXPath(value)).Append(']');
            return this;
        }

        // Enthält-Filter für Attribut: contains(@name, 'value')
        public XPathBuilder AttributeContains(string name, string value)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Attributname darf nicht leer sein", nameof(name));
            _sb.Append('[').Append("contains(@").Append(name).Append(",").Append(EscapeForXPath(value)).Append(")]");
            return this;
        }

        // Text-Contain-Filter: contains(normalize-space(.), 'text')
        public XPathBuilder ContainsText(string text)
        {
            _sb.Append('[').Append("contains(normalize-space(.),").Append(EscapeForXPath(text)).Append(")]");
            return this;
        }

        // Exakter Textvergleich: text() = 'value'
        public XPathBuilder TextEquals(string text)
        {
            _sb.Append('[').Append("text()=").Append(EscapeForXPath(text)).Append(']');
            return this;
        }

        // Indexierung: [n] (1-based)
        public XPathBuilder Index(int oneBasedIndex)
        {
            if (oneBasedIndex <= 0) throw new ArgumentOutOfRangeException(nameof(oneBasedIndex));
            _sb.Append('[').Append(oneBasedIndex).Append(']');
            return this;
        }

        // Add raw segment (z.B. "div[1]/span")
        public XPathBuilder Segment(string segment)
        {
            if (string.IsNullOrEmpty(segment)) throw new ArgumentException("Segment darf nicht leer sein", nameof(segment));
            // Wenn das Segment mit / oder // beginnt, einfach anhängen, sonst als Child behandeln
            if (segment.StartsWith("//") || segment.StartsWith("./") || segment.StartsWith("/"))
            {
                _sb.Append(segment);
            }
            else
            {
                _sb.Append('/').Append(segment);
            }

            return this;
        }

        public override string ToString() => Build();

        public string Build() => _sb.ToString();

        private static void EnsureName(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Name darf nicht leer sein", nameof(name));
        }

        // Escaping für XPath-Literale: verwendet simple '...' falls möglich, sonst concat(...,'"'",...)
        private static string EscapeForXPath(string value)
        {
            if (value == null) return "''";
            if (!value.Contains("'")) return "'" + value + "'";

            // Split by single-quote and join with ,"'",
            var parts = value.Split('\'');
            // parts kann leere Strings enthalten -> immer als quoted part darstellen
            var escapedParts = parts.Select(p => "'" + p + "'").ToArray();
            return "concat(" + string.Join(",\"'\",", escapedParts) + ")";
        }
    }
}
