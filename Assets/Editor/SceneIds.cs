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
            Dictionary<long, string> keys = StableKeys(docs, byId);

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

        // A stable key for every GameObject and component, assigned by walking the hierarchy in
        // its saved order: roots as SceneRoots lists them, children as each m_Children lists them.
        // A GameObject's key is its parent's key plus its name, with "#n" for the nth same-named
        // sibling; a component's key is its GameObject's key plus class, script and ordinal.
        private static Dictionary<long, string> StableKeys(List<Doc> docs, Dictionary<long, Doc> byId)
        {
            var ownerOf = new Dictionary<long, long>();
            var rootTransforms = new List<long>();
            foreach (Doc doc in docs)
            {
                Match owner = Owner.Match(doc.Body);
                Match father = Father.Match(doc.Body);
                if (owner.Success && father.Success)
                {
                    ownerOf[doc.Id] = long.Parse(owner.Groups[1].Value);
                    if (father.Groups[1].Value == "0")
                    {
                        rootTransforms.Add(doc.Id);
                    }
                }
            }

            // Saved scenes list their roots; a prefab has one root and no SceneRoots document.
            Doc sceneRoots = docs.FirstOrDefault(d => d.Body.Contains("\n  m_Roots:"));
            List<long> roots = sceneRoots != null ? IdList(sceneRoots.Body, "m_Roots") : rootTransforms;

            var keys = new Dictionary<long, string>();
            void Visit(List<long> transforms, string parentKey)
            {
                var twins = new Dictionary<string, int>();
                foreach (long transformId in transforms)
                {
                    if (!ownerOf.TryGetValue(transformId, out long gameObjectId) || !byId.TryGetValue(gameObjectId, out Doc gameObject))
                    {
                        continue;
                    }

                    string name = NameOf(gameObject);
                    twins.TryGetValue(name, out int twin);
                    twins[name] = twin + 1;
                    string key = parentKey + "/" + name + (twin == 0 ? string.Empty : "#" + twin);
                    keys[gameObjectId] = key;
                    AddComponentKeys(gameObject, key, byId, keys);
                    Visit(IdList(byId[transformId].Body, "m_Children"), key);
                }
            }

            Visit(roots, string.Empty);
            return keys;
        }

        private static void AddComponentKeys(Doc gameObject, string key, Dictionary<long, Doc> byId, Dictionary<long, string> keys)
        {
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
                keys[componentId] = key + "|" + kind + "|" + ordinal;
            }
        }

        // The file IDs listed under a top-level field such as "m_Children:" or "m_Roots:".
        private static List<long> IdList(string body, string field)
        {
            Match list = Regex.Match(body, @"^  " + field + @":[ \t]*\r?\n((?:  - \{fileID: -?\d+\}\r?\n?)*)", RegexOptions.Multiline);
            var ids = new List<long>();
            if (list.Success)
            {
                foreach (Match entry in LocalReference.Matches(list.Groups[1].Value))
                {
                    ids.Add(long.Parse(entry.Groups[1].Value));
                }
            }

            return ids;
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
