using System;
using System.Globalization;
using System.IO;
using System.Text;
using Etoos.DetectSample.Alerts;
using Etoos.DetectSample.Config;

namespace Etoos.DetectSample.Logging
{
    /// <summary>
    /// 알림을 <c>alerts-yyyyMMdd.jsonl</c> 에 1줄씩 append 한다(JSON Lines).
    ///
    /// 설계
    ///  - 알림은 드물다(분 단위). 그래서 파일 핸들을 열어두지 않고 <b>쓸 때마다 열고 닫는다</b>.
    ///    장점: 앱이 강제 종료돼도 기록이 남고, 날짜 롤오버가 자동으로 처리된다.
    ///  - <b>어떤 예외도 밖으로 던지지 않는다.</b> 로그 실패로 앱이 죽으면 안 된다(CONTRACT.md 5절).
    ///    실패는 onError 콜백으로만 보고한다.
    ///  - UTF-8 <b>BOM 없이</b> 기록한다(JSONL 파서 호환).
    /// </summary>
    public sealed class AlertJsonlLogger
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly object _sync = new object();
        private readonly string _directory;
        private readonly Action<string> _onError;

        /// <summary>기록 성공 줄 수 / 실패 횟수(디버그용).</summary>
        public int WrittenCount;
        public int FailedCount;

        public AlertJsonlLogger(AppSettings settings, Action<string> onError)
        {
            AppSettings s = settings != null ? settings : new AppSettings();
            _directory = LogPaths.ResolveDirectory(s.LogDirectory);
            _onError = onError;
        }

        /// <summary>현재 날짜 기준 파일 경로. 자정을 넘기면 자동으로 다음 파일이 된다.</summary>
        public string CurrentFilePath
        {
            get
            {
                return Path.Combine(_directory,
                    "alerts-" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".jsonl");
            }
        }

        /// <summary>AlertDispatcher.AddSink("jsonl", logger.Write) 로 연결해서 쓴다.</summary>
        public void Write(AlertEvent e)
        {
            if (e == null) return;

            string line;
            try
            {
                line = AlertJson.ToJson(e);
            }
            catch (Exception ex)
            {
                Fail("알림 JSON 직렬화 실패: " + ex.Message);
                return;
            }

            lock (_sync)
            {
                try
                {
                    if (!LogPaths.EnsureDirectory(_directory))
                    {
                        Fail("로그 디렉토리를 만들 수 없습니다: " + _directory);
                        return;
                    }

                    // append + 공유 읽기 허용 (기록 중에도 tail 로 볼 수 있게)
                    using (FileStream fs = new FileStream(CurrentFilePath, FileMode.Append,
                                                          FileAccess.Write, FileShare.Read))
                    using (StreamWriter w = new StreamWriter(fs, Utf8NoBom))
                    {
                        w.Write(line);
                        w.Write('\n');   // JSONL 은 LF 고정 (Windows CRLF 로 깨지지 않게)
                    }
                    WrittenCount++;
                }
                catch (Exception ex)
                {
                    Fail("알림 로그 기록 실패: " + ex.Message);
                }
            }
        }

        private void Fail(string message)
        {
            FailedCount++;
            if (_onError == null) return;
            try { _onError(message); }
            catch (Exception) { /* 에러 콜백 실패는 무시 */ }
        }
    }
}
