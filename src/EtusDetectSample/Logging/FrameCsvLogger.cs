using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Etoos.DetectSample.Analysis;
using Etoos.DetectSample.Config;

namespace Etoos.DetectSample.Logging
{
    /// <summary>
    /// 프레임별 관측/판정값을 CSV 로 남긴다. 임계값 재보정의 근거 자료가 되는 산출물이다.
    ///
    /// [스레딩] <b>워커 스레드를 절대 막지 않는다.</b>
    /// Log() 는 문자열 1줄을 만들어 내부 Queue 에 넣고 즉시 돌아온다.
    /// 실제 디스크 쓰기는 백그라운드 flush 스레드가 담당한다.
    ///
    /// [실패 정책] 파일 I/O 실패로 앱이 죽으면 안 된다(CONTRACT.md 5절).
    /// 모든 예외를 삼키고 onError 콜백으로만 보고한다. 같은 실패를 반복 보고하지 않는다.
    ///
    /// [NaN] 빈 칸으로 남긴다(pandas/Excel 에서 결측으로 읽힌다).
    /// [bool] 1/0 으로 남긴다(그래프 그리기 쉽게).
    /// </summary>
    public sealed class FrameCsvLogger : IDisposable
    {
        public const string Header =
            "ts,wallClock,frameIndex,faceDetected,faceCount,faceId,boxX,boxY,boxW,boxH," +
            "yaw,pitch,roll,landmarkConf,maskConf,isMasked,occlL,occlR,occlM,fineOccl," +
            "eyelidL,eyelidR,eyelidValid,eyeState,unknownReason,state,slump,closedSec,noFaceSec,perclos," +
            "seatedSec,studySec,drowsySec,awaySec,unknownSec,sdkMs";

        /// <summary>flush 스레드가 큐를 다시 확인하는 주기(ms). 판정 임계값이 아니다.</summary>
        private const int FlushIntervalMs = 500;
        /// <summary>큐 상한(줄). 디스크가 느리거나 막혔을 때 메모리가 무한히 늘지 않게 하는 안전장치.</summary>
        private const int MaxQueuedLines = 200000;
        /// <summary>Dispose 시 flush 스레드를 기다리는 최대 시간(ms).</summary>
        private const int ShutdownJoinMs = 3000;

        private static readonly UTF8Encoding Utf8WithBom = new UTF8Encoding(true);

        private readonly object _sync = new object();      // _queue 보호
        private readonly object _writeSync = new object(); // _writer 보호
        private readonly Queue<string> _queue = new Queue<string>();
        private readonly Action<string> _onError;
        private readonly AppSettings _settings;
        private readonly string _directory;
        private readonly string _sessionStamp;
        private readonly DateTime _startedAt;
        private readonly bool _enabled;

        private Thread _thread;
        private volatile bool _stopping;
        private StreamWriter _writer;
        private bool _writerFailed;   // 한 번 실패하면 재시도하지 않는다(로그 폭주 방지)
        private bool _dropReported;
        private bool _disposed;

        public int WrittenLines;
        public int DroppedLines;

        public FrameCsvLogger(AppSettings settings, Action<string> onError)
        {
            _settings = settings != null ? settings : new AppSettings();
            _onError = onError;
            _startedAt = DateTime.Now;
            _sessionStamp = _startedAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            _directory = LogPaths.ResolveDirectory(_settings.LogDirectory);
            _enabled = _settings.EnableFrameCsv;

            if (_enabled)
            {
                _thread = new Thread(FlushLoop);
                _thread.IsBackground = true;   // 앱 종료를 막지 않는다
                _thread.Name = "FrameCsvLogger";
                _thread.Start();
            }
        }

        /// <summary>AppSettings.EnableFrameCsv 가 false 면 Log() 는 아무것도 하지 않는다.</summary>
        public bool Enabled { get { return _enabled; } }

        public string FilePath
        {
            get { return Path.Combine(_directory, "frames-" + _sessionStamp + ".csv"); }
        }

        public string SessionFilePath
        {
            get { return Path.Combine(_directory, "session-" + _sessionStamp + ".csv"); }
        }

        /// <summary>워커 스레드에서 호출. 큐에 넣기만 하고 즉시 반환한다.</summary>
        public void Log(SeatSnapshot snapshot)
        {
            if (!_enabled || _disposed || snapshot == null) return;

            string line;
            try
            {
                line = BuildLine(snapshot);
            }
            catch (Exception ex)
            {
                Fail("CSV 줄 생성 실패: " + ex.Message);
                return;
            }

            lock (_sync)
            {
                if (_queue.Count >= MaxQueuedLines)
                {
                    DroppedLines++;
                    if (!_dropReported)
                    {
                        _dropReported = true;
                        Fail("CSV 큐가 가득 찼습니다(" + MaxQueuedLines.ToString(CultureInfo.InvariantCulture) +
                             "줄). 이후 프레임 로그를 버립니다.");
                    }
                    return;
                }
                _queue.Enqueue(line);
                Monitor.Pulse(_sync);
            }
        }

        /// <summary>큐에 남은 것을 지금 디스크로 내린다. 호출 스레드에서 쓰기가 일어난다.</summary>
        public void Flush()
        {
            if (!_enabled) return;

            List<string> batch = null;
            lock (_sync)
            {
                if (_queue.Count > 0)
                {
                    batch = new List<string>(_queue.Count);
                    while (_queue.Count > 0) batch.Add(_queue.Dequeue());
                }
            }
            if (batch != null) WriteBatch(batch);

            lock (_writeSync)
            {
                try { if (_writer != null) _writer.Flush(); }
                catch (Exception ex) { Fail("CSV flush 실패: " + ex.Message); }
            }
        }

        /// <summary>
        /// 세션 종료 요약 1행. EnableFrameCsv 와 무관하게 항상 시도한다(가벼우면서 가치가 크다).
        /// 실패하면 null 을 돌려준다.
        /// </summary>
        public string WriteSessionSummary(SessionStats stats, SeatSnapshot last)
        {
            if (stats == null) return null;

            try
            {
                if (!LogPaths.EnsureDirectory(_directory))
                {
                    Fail("로그 디렉토리를 만들 수 없습니다: " + _directory);
                    return null;
                }

                DateTime endedAt = DateTime.Now;
                double observedSec = (last != null && last.Observation != null) ? last.Observation.MonotonicSec : 0.0;
                long frames = (last != null && last.Observation != null) ? last.Observation.FrameIndex + 1 : 0;

                StringBuilder sb = new StringBuilder(512);
                sb.Append("sessionStart,sessionEnd,observedSec,frames,seatId,")
                  .Append("seatedSec,studySec,drowsySec,awaySec,unknownSec,")
                  .Append("drowsyCount,awayCount,lastState,settings\n");

                sb.Append(S(_startedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))).Append(',');
                sb.Append(S(endedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))).Append(',');
                sb.Append(D(observedSec, 2)).Append(',');
                sb.Append(frames.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(S(_settings.SeatId)).Append(',');
                sb.Append(D(stats.SeatedSec, 2)).Append(',');
                sb.Append(D(stats.StudySec, 2)).Append(',');
                sb.Append(D(stats.DrowsySec, 2)).Append(',');
                sb.Append(D(stats.AwaySec, 2)).Append(',');
                sb.Append(D(stats.UnknownSec, 2)).Append(',');
                sb.Append(stats.DrowsyCount.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(stats.AwayCount.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(S(last != null ? last.State.ToString() : "")).Append(',');
                sb.Append(S(_settings.ToString())).Append('\n');

                string path = SessionFilePath;
                File.WriteAllText(path, sb.ToString(), Utf8WithBom);
                return path;
            }
            catch (Exception ex)
            {
                Fail("세션 요약 기록 실패: " + ex.Message);
                return null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _stopping = true;
            lock (_sync) { Monitor.PulseAll(_sync); }

            Thread t = _thread;
            _thread = null;
            if (t != null)
            {
                try { t.Join(ShutdownJoinMs); }
                catch (Exception) { /* 종료 경로에서는 아무것도 던지지 않는다 */ }
            }

            // flush 스레드가 시간 안에 못 끝냈어도 남은 것을 여기서 비운다.
            try { FlushRemaining(); }
            catch (Exception) { }

            lock (_writeSync)
            {
                try
                {
                    if (_writer != null)
                    {
                        _writer.Flush();
                        _writer.Dispose();
                    }
                }
                catch (Exception) { }
                _writer = null;
            }
        }

        // ------------------------------------------------------------------
        // 내부
        // ------------------------------------------------------------------

        private void FlushRemaining()
        {
            List<string> batch = null;
            lock (_sync)
            {
                if (_queue.Count > 0)
                {
                    batch = new List<string>(_queue.Count);
                    while (_queue.Count > 0) batch.Add(_queue.Dequeue());
                }
            }
            if (batch != null) WriteBatch(batch);
        }

        private void FlushLoop()
        {
            while (true)
            {
                List<string> batch = null;
                lock (_sync)
                {
                    while (_queue.Count == 0 && !_stopping)
                        Monitor.Wait(_sync, FlushIntervalMs);

                    if (_queue.Count > 0)
                    {
                        batch = new List<string>(_queue.Count);
                        while (_queue.Count > 0) batch.Add(_queue.Dequeue());
                    }
                    else if (_stopping)
                    {
                        break;
                    }
                }

                if (batch != null) WriteBatch(batch);
            }
        }

        private void WriteBatch(List<string> lines)
        {
            if (lines == null || lines.Count == 0) return;

            lock (_writeSync)
            {
                if (_writerFailed) return;   // 이미 포기한 상태 — 조용히 버린다
                try
                {
                    if (!EnsureWriterUnsafe()) return;
                    for (int i = 0; i < lines.Count; i++) _writer.WriteLine(lines[i]);
                    _writer.Flush();
                    WrittenLines += lines.Count;
                }
                catch (Exception ex)
                {
                    _writerFailed = true;
                    Fail("CSV 기록 실패(이후 프레임 로그를 중단합니다): " + ex.Message);
                    try { if (_writer != null) _writer.Dispose(); }
                    catch (Exception) { }
                    _writer = null;
                }
            }
        }

        /// <summary>_writeSync 를 잡은 상태에서만 호출한다.</summary>
        private bool EnsureWriterUnsafe()
        {
            if (_writer != null) return true;
            if (_writerFailed) return false;

            if (!LogPaths.EnsureDirectory(_directory))
            {
                _writerFailed = true;
                Fail("로그 디렉토리를 만들 수 없습니다: " + _directory);
                return false;
            }

            string path = FilePath;
            bool isNew = !File.Exists(path);
            FileStream fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            _writer = new StreamWriter(fs, Utf8WithBom);
            _writer.AutoFlush = false;
            if (isNew) _writer.WriteLine(Header);
            return true;
        }

        private string BuildLine(SeatSnapshot s)
        {
            FrameObservation o = s.Observation;
            StringBuilder sb = new StringBuilder(320);

            if (o == null)
            {
                // 관측값 없이 스냅샷만 들어온 경우(정상 흐름에서는 발생하지 않음) — 앞 컬럼을 비운다.
                for (int i = 0; i < 23; i++) sb.Append(',');
            }
            else
            {
                sb.Append(D(o.MonotonicSec, 3)).Append(',');
                sb.Append(S(o.WallClock == default(DateTime)
                    ? ""
                    : o.WallClock.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))).Append(',');
                sb.Append(o.FrameIndex.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(B(o.FaceDetected)).Append(',');
                sb.Append(o.FaceCount.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(o.FaceTrackId.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(F(o.BoxX, 1)).Append(',');
                sb.Append(F(o.BoxY, 1)).Append(',');
                sb.Append(F(o.BoxW, 1)).Append(',');
                sb.Append(F(o.BoxH, 1)).Append(',');
                sb.Append(F(o.Yaw, 2)).Append(',');
                sb.Append(F(o.Pitch, 2)).Append(',');
                sb.Append(F(o.Roll, 2)).Append(',');
                sb.Append(F(o.LandmarkConfidence, 3)).Append(',');
                sb.Append(F(o.MaskConfidence, 3)).Append(',');
                sb.Append(B(o.IsMasked)).Append(',');
                sb.Append(F(o.OcclusionLeftEye, 3)).Append(',');
                sb.Append(F(o.OcclusionRightEye, 3)).Append(',');
                sb.Append(F(o.OcclusionMouth, 3)).Append(',');
                sb.Append(F(o.FineOcclusion, 3)).Append(',');
                sb.Append(F(o.EyelidLeft, 3)).Append(',');
                sb.Append(F(o.EyelidRight, 3)).Append(',');
                sb.Append(B(o.EyelidValid)).Append(',');
            }

            sb.Append(S(s.Eye.State.ToString())).Append(',');
            sb.Append(S(s.Eye.Reason == UnknownReason.None ? "" : s.Eye.Reason.ToString())).Append(',');
            sb.Append(S(s.State.ToString())).Append(',');
            sb.Append(B(s.SlumpSuspected)).Append(',');
            sb.Append(D(s.ClosedEyeSec, 2)).Append(',');
            sb.Append(D(s.NoFaceSec, 2)).Append(',');
            sb.Append(D(s.Perclos, 3)).Append(',');
            sb.Append(D(s.SeatedSec, 2)).Append(',');
            sb.Append(D(s.StudySec, 2)).Append(',');
            sb.Append(D(s.DrowsySec, 2)).Append(',');
            sb.Append(D(s.AwaySec, 2)).Append(',');
            sb.Append(D(s.UnknownSec, 2)).Append(',');
            sb.Append(o == null ? "" : D(o.SdkElapsedMs, 2));

            return sb.ToString();
        }

        /// <summary>NaN/Infinity 는 빈 칸.</summary>
        private static string D(double v, int digits)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "";
            return v.ToString("F" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        private static string F(float v, int digits)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return "";
            return ((double)v).ToString("F" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        private static string B(bool v)
        {
            return v ? "1" : "0";
        }

        /// <summary>CSV 이스케이프. 콤마/따옴표/개행이 들어와도 컬럼이 밀리지 않게 한다.</summary>
        private static string S(string v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            if (v.IndexOf(',') < 0 && v.IndexOf('"') < 0 && v.IndexOf('\n') < 0 && v.IndexOf('\r') < 0)
                return v;
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        }

        private void Fail(string message)
        {
            if (_onError == null) return;
            try { _onError(message); }
            catch (Exception) { /* 에러 콜백 실패는 무시 */ }
        }
    }

    /// <summary>
    /// 로그 경로 공용 헬퍼. FrameCsvLogger / AlertJsonlLogger 가 함께 쓴다.
    /// (별도 파일을 만들지 않기 위해 여기 둔다 — 소유 파일 목록 밖의 파일을 만들지 않는다.)
    /// </summary>
    internal static class LogPaths
    {
        /// <summary>
        /// 상대 경로는 <b>현재 작업 디렉토리가 아니라 실행 파일 위치</b> 기준으로 푼다.
        /// WinForms 앱은 바로가기/파일 대화상자 때문에 CWD 가 바뀔 수 있어서
        /// 로그가 엉뚱한 곳에 흩어지는 것을 막는다.
        /// </summary>
        public static string ResolveDirectory(string dir)
        {
            string d = string.IsNullOrEmpty(dir) ? "logs" : dir;
            try
            {
                if (Path.IsPathRooted(d)) return d;
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, d);
            }
            catch (Exception)
            {
                return d;
            }
        }

        /// <summary>없으면 만든다. 실패해도 예외를 던지지 않고 false 를 돌려준다.</summary>
        public static bool EnsureDirectory(string dir)
        {
            try
            {
                if (string.IsNullOrEmpty(dir)) return false;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
