using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SimoSlime.Editor
{
    /// <summary>
    /// Unity gives every object in a saved scene or prefab a random local file ID, so saving the
    /// same generated scene twice rewrites the whole file. <see cref="Stabilise"/> replaces each
    /// GameObject's and component's ID with a hash of where it sits in the hierarchy and sorts
    /// the documents by ID, as Unity does, so the same scene always saves to the same text.
    /// Settings documents, SceneRoots and references into other files keep their IDs.
    /// </summary>
    public static class SceneIds
    {
        private const string GameObjectClass = "1";
        private const long SmallestId = 1_000_000;

        private static readonly Regex Header = new Regex(@"^--- !u!(\d+) &(-?\d+)(.*)$", RegexOptions.Multiline);
        private static readonly Regex LocalReference = new Regex(@"\{fileID: (-?\d+)\}");
        private static readonly Regex Owner = new Regex(@"^  m_GameObject: \{fileID: (-?\d+)\}", RegexOptions.Multiline);
        private static readonly Regex Father = new Regex(@"^  m_Father: \{fileID: (-?\d+)\}", RegexOptions.Multiline);
        private static readonly Regex Name = new Regex(@"^  m_Name: ?(.*?)\r?$", RegexOptions.Multiline);
        private static readonly Regex Script = new Regex(@"^  m_Script: \{fileID: -?\d+, guid: ([0-9a-fA-F]+)", RegexOptions.Multiline);
        private static readonly Regex Component = new Regex(@"^  - component: \{fileID: (-?\d+)\}", RegexOptions.Multiline);

        private sealed class Doc
        {
            public string ClassId;
            public long Id;
            public string HeaderRest;
            public string Body;
            public long NewId;
        }

        public static string Stabilise(string yaml)
        {
            MatchCollection headers = Header.Matches(yaml);
            if (headers.Count == 0)
            {
                return yaml;
            }

            string preamble = yaml.Substring(0, headers[0].Index);
            var docs = new List<Doc>();
            for (int i = 0; i < headers.Count; i++)
            {
                Match header = headers[i];
                int bodyStart = header.Index + header.Length;
                int end = i + 1 < headers.Count ? headers[i + 1].Index : yaml.Length;
                docs.Add(new Doc
                {
                    ClassId = header.Groups[1].Value,
                    Id = long.Parse(header.Groups[2].Value),
                    HeaderRest = header.Groups[3].Value,
                    Body = yaml.Substring(bodyStart, end - bodyStart),
                });
            }

            Dictionary<long, Doc> byId = docs.ToDictionary(d => d.Id);
            Dictionary<long, string> keys = Keys(docs, byId);

            var used = new HashSet<long>(docs.Where(d => !keys.ContainsKey(d.Id)).Select(d => d.Id));
            var map = new Dictionary<long, long>();
            foreach (KeyValuePair<long, string> pair in keys.OrderBy(p => p.Value, StringComparer.Ordinal))
            {
                long id = Hash(pair.Value);
                while (!used.Add(id))
                {
                    id = Hash(pair.Value + "+" + id);
                }

                map[pair.Key] = id;
            }

            foreach (Doc doc in docs)
            {
                doc.NewId = map.TryGetValue(doc.Id, out long newId) ? newId : doc.Id;
                doc.Body = LocalReference.Replace(doc.Body, m =>
                {
                    long target = long.Parse(m.Groups[1].Value);
                    return map.TryGetValue(target, out long moved) ? "{fileID: " + moved + "}" : m.Value;
                });
            }

            var output = new StringBuilder(preamble);
            foreach (Doc doc in docs.OrderBy(d => d.NewId))
            {
                output.Append("--- !u!").Append(doc.ClassId).Append(" &").Append(doc.NewId).Append(doc.HeaderRest).Append(doc.Body);
            }

            return output.ToString();
        }

        // A stable key for every GameObject (its path of names) and component (its GameObject's
        // path, class, script and position among same-kind components on that GameObject).
        private static Dictionary<long, string> Keys(List<Doc> docs, Dictionary<long, Doc> byId)
        {
            var gameObjects = docs.Where(d => d.ClassId == GameObjectClass).ToList();
            var transformOf = new Dictionary<long, Doc>();
            foreach (Doc doc in docs)
            {
                Match owner = Owner.Match(doc.Body);
                if (owner.Success && Father.IsMatch(doc.Body))
                {
                    transformOf[long.Parse(owner.Groups[1].Value)] = doc;
                }
            }

            var paths = new Dictionary<long, string>();
            string PathOf(Doc gameObject)
            {
                if (paths.TryGetValue(gameObject.Id, out string known))
                {
                    return known;
                }

                string parentPath = string.Empty;
                if (transformOf.TryGetValue(gameObject.Id, out Doc transform))
                {
                    long fatherId = long.Parse(Father.Match(transform.Body).Groups[1].Value);
                    if (byId.TryGetValue(fatherId, out Doc father) && Owner.Match(father.Body) is Match m && m.Success
                        && byId.TryGetValue(long.Parse(m.Groups[1].Value), out Doc parent))
                    {
                        parentPath = PathOf(parent);
                    }
                }

                string path = parentPath + "/" + NameOf(gameObject);
                paths[gameObject.Id] = path;
                return path;
            }

            var keys = new Dictionary<long, string>();
            var seen = new Dictionary<string, int>();
            foreach (Doc gameObject in gameObjects.OrderBy(g => SiblingOrder(g, transformOf, byId)))
            {
                string path = PathOf(gameObject);
                seen.TryGetValue(path, out int twins);
                seen[path] = twins + 1;
                keys[gameObject.Id] = twins == 0 ? path : path + "#" + twins;

                var kinds = new Dictionary<string, int>();
                foreach (Match component in Component.Matches(gameObject.Body))
                {
                    long componentId = long.Parse(component.Groups[1].Value);
                    if (!byId.TryGetValue(componentId, out Doc doc))
                    {
                        continue;
                    }

                    Match script = Script.Match(doc.Body);
                    string kind = doc.ClassId + (script.Success ? ":" + script.Groups[1].Value : string.Empty);
                    kinds.TryGetValue(kind, out int ordinal);
                    kinds[kind] = ordinal + 1;
                    keys[componentId] = keys[gameObject.Id] + "|" + kind + "|" + ordinal;
                }
            }

            return keys;
        }

        // Where a GameObject sits among its siblings, so same-named twins get a stable "#n".
        private static int SiblingOrder(Doc gameObject, Dictionary<long, Doc> transformOf, Dictionary<long, Doc> byId)
        {
            if (!transformOf.TryGetValue(gameObject.Id, out Doc transform))
            {
                return 0;
            }

            long fatherId = long.Parse(Father.Match(transform.Body).Groups[1].Value);
            string list = byId.TryGetValue(fatherId, out Doc father) ? father.Body
                : byId.Values.FirstOrDefault(d => d.Body.Contains("m_Roots:"))?.Body ?? string.Empty;
            int at = list.IndexOf("{fileID: " + transform.Id + "}", StringComparison.Ordinal);
            return at < 0 ? int.MaxValue : at;
        }

        private static string NameOf(Doc gameObject)
        {
            Match name = Name.Match(gameObject.Body);
            return name.Success ? name.Groups[1].Value : string.Empty;
        }

        // FNV-1a, 64-bit, kept positive and clear of the small IDs Unity reserves for settings.
        private static long Hash(string key)
        {
            ulong hash = 14695981039346656037;
            foreach (byte b in Encoding.UTF8.GetBytes(key))
            {
                hash ^= b;
                hash *= 1099511628211;
            }

            long id = (long)(hash & 0x7FFFFFFFFFFFFFFF);
            return id < SmallestId ? id + SmallestId : id;
        }
    }
}
