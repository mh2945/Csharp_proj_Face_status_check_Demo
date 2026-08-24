using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Etoos.DetectSample.Tests.Support
{
    /// <summary>테스트용 임시 디렉토리. using 블록이 끝나면 통째로 지운다.</summary>
    public sealed class TempDir : IDisposable
    {
        public readonly string Path;

        public TempDir()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "etus-detect-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        /// <summary>디렉토리로 쓸 수 없는 경로(일반 파일 아래). I/O 실패 경로 테스트용.</summary>
        public string BlockedSubPath()
        {
            string file = System.IO.Path.Combine(Path, "not-a-directory");
            File.WriteAllText(file, "이 파일 때문에 하위 디렉토리를 만들 수 없다");
            return System.IO.Path.Combine(file, "logs");
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, true);
            }
            catch (Exception)
            {
                // 정리 실패로 테스트를 깨뜨리지 않는다.
            }
        }
    }

    /// <summary>따옴표를 인식하는 최소 CSV 분해기. 컬럼 수 검증에만 쓴다.</summary>
    public static class Csv
    {
        public static IList<string> SplitLine(string line)
        {
            List<string> fields = new List<string>();
            StringBuilder cur = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else cur.Append(c);
                }
                else
                {
                    if (c == '"') inQuotes = true;
                    else if (c == ',') { fields.Add(cur.ToString()); cur.Length = 0; }
                    else cur.Append(c);
                }
            }
            fields.Add(cur.ToString());
            return fields;
        }
    }
}
