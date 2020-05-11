using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace PewsClient
{
    class OptionBase
    {
        private Dictionary<string, string> m_data = new Dictionary<string, string>();
        public IReadOnlyDictionary<string, string> Data => m_data;

        public void Load(string fileName)
        {
            m_data.Clear();

            if (!File.Exists(fileName))
            {
                return;
            }

            using (var sr = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                string currentKey = null;
                var buffer = new StringBuilder();

                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();

                    if (line.Length >= 2 && line[0] == '[' && line[line.Length - 1] == ']')
                    {
                        if (currentKey != null)
                        {
                            m_data.Add(currentKey, buffer.ToString());
                        }

                        currentKey = line.Substring(1, line.Length - 2);
                        buffer.Clear();
                    }
                    else
                    {
                        if (buffer.Length > 0)
                        {
                            buffer.AppendLine();
                        }
                        
                        buffer.Append(line);
                    }
                }

                if (currentKey != null)
                {
                    m_data.Add(currentKey, buffer.ToString());
                }
            }

            AfterLoad();
        }

        public void Save(string fileName)
        {
            BeforeSave();

            using (var sw = new StreamWriter(new FileStream(fileName, FileMode.Create)))
            {
                foreach (var kv in m_data)
                {
                    sw.WriteLine();
                    sw.Write($"[{kv.Key}]");
                    if (kv.Value.Length > 0)
                    {
                        sw.WriteLine();
                        sw.Write(kv.Value);
                    }
                }
            }
        }

        public void Clear()
        {
            m_data.Clear();
        }

        protected virtual void AfterLoad() { }
        protected virtual void BeforeSave() { }

        protected T GetProperty<T>(string key, Func<string, T> parser, T defaultValue = default)
        {
            if (m_data.TryGetValue(key, out string val))
            {
                return parser(val);
            }

            return defaultValue;
        }

        protected void SetProperty<T>(string key, T val, Func<T, string> stringifier = null)
        {
            string strVal = (stringifier == null) ? val.ToString() : stringifier(val);

            if (m_data.ContainsKey(key))
            {
                m_data[key] = strVal;
            }
            else
            {
                m_data.Add(key, strVal);
            }
        }
    }
}
