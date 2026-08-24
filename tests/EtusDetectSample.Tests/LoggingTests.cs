using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Etus.DetectSample.Alerts;
using Etus.DetectSample.Analysis;
using Etus.DetectSample.Config;
using Etus.DetectSample.Logging;
using Etus.DetectSample.Tests.Support;
using Xunit;

namespace Etus.DetectSample.Tests
{
    /// <summary>
    /// 로그 산출물(frames CSV / alerts JSONL / session CSV).
    /// 데이터 산출 가능성 증명이 PoC 의 목적 중 하나라 "파일에 실제로 내용이 있는지"가 요구사항이다.
    /// 동시에 <b>로그 실패로 앱이 죽으면 안 된다</b>(CONTRACT.md 5절).
    /// </summary>
    public class LoggingTests
    {
        private static AppSettings SettingsFor(string logDir)
        {
            AppSettings s = new AppSettings();
            s.LogDirectory = logDir;
            s.EnableFrameCsv = true;
            return s;
        }

        /// <summary>실제 상태머신을 한 번 돌려 스냅샷을 얻는다.</summary>
        private static Harness RunSample()
        {
            Harness h = new Harness();
            h.Run(Seq.Start().Open(1.0).Closed(5.0).Open(1.0).NoFace(1.0));
            return h;
        }

        // ==================================================================
        // FrameCsvLogger
        // ==================================================================

        [Fact(DisplayName = "CSV — Flush() 후 파일에 헤더와 데이터가 실제로 있다")]
        public void FrameCsv_WritesRealContent()
        {
            using (TempDir dir = new TempDir())
            {
                Harness h = RunSample();
                string path;

                using (FrameCsvLogger log = new FrameCsvLogger(SettingsFor(dir.Path), null))
                {
                    for (int i = 0; i < h.Snapshots.Count; i++) log.Log(h.Snapshots[i]);
                    log.Flush();
                    path = log.FilePath;

                    Assert.True(File.Exists(path), "Flush() 후에도 파일이 없다: " + path);
                    string[] afterFlush = File.ReadAllLines(path);
                    Assert.True(afterFlush.Length >= h.Snapshots.Count + 1,
                                "줄 수가 부족하다: " + afterFlush.Length.ToString());
                }

                string[] lines = File.ReadAllLines(path);
                Assert.Equal(FrameCsvLogger.Header, lines[0]);
                Assert.Equal(h.Snapshots.Count + 1, lines.Length);   // 헤더 1줄 + 프레임 수
            }
        }

        [Fact(DisplayName = "CSV — 헤더 컬럼 수와 데이터 행 컬럼 수가 일치한다")]
        public void FrameCsv_ColumnCountsMatch()
        {
            using (TempDir dir = new TempDir())
            {
                Harness h = RunSample();
                string path;
                using (FrameCsvLogger log = new FrameCsvLogger(SettingsFor(dir.Path), null))
                {
                    for (int i = 0; i < h.Snapshots.Count; i++) log.Log(h.Snapshots[i]);
                    path = log.FilePath;
                }

                string[] lines = File.ReadAllLines(path);
                int headerCols = Csv.SplitLine(lines[0]).Count;
                Assert.Equal(37, headerCols);

                for (int i = 1; i < lines.Length; i++)
                {
                    int cols = Csv.SplitLine(lines[i]).Count;
                    Assert.True(headerCols == cols,
                                "행 " + i.ToString() + " 컬럼 수 " + cols.ToString() +
                                " != 헤더 " + headerCols.ToString());
                }
            }
        }

        [Fact(DisplayName = "CSV — NaN 은 빈 칸으로 나간다")]
        public void FrameCsv_NaNBecomesEmptyCell()
        {
            using (TempDir dir = new TempDir())
            {
                // 속성 미측정(NaN) 프레임을 그대로 흘린다.
                Harness h = new Harness();
                Seq seq = Seq.Start().Custom(0.125, delegate(FrameObservation o)
                {
                    o.LandmarkConfidence = float.NaN;
                    o.MaskConfidence = float.NaN;
                    o.OcclusionLeftEye = float.NaN;
                    o.OcclusionRightEye = float.NaN;
                    o.OcclusionMouth = float.NaN;
                    o.FineOcclusion = float.NaN;
                });
                h.Run(seq);

                string path;
                using (FrameCsvLogger log = new FrameCsvLogger(SettingsFor(dir.Path), null))
                {
                    log.Log(h.Snapshots[0]);
                    path = log.FilePath;
                }

                string[] lines = File.ReadAllLines(path);
                IList<string> header = Csv.SplitLine(lines[0]);
                IList<string> row = Csv.SplitLine(lines[1]);

                Assert.Equal("", row[header.IndexOf("landmarkConf")]);
                Assert.Equal("", row[header.IndexOf("maskConf")]);
                Assert.Equal("", row[header.IndexOf("occlL")]);
                Assert.Equal("", row[header.IndexOf("fineOccl")]);
                // NaN 이라는 글자가 CSV 에 들어가면 pandas/Excel 쪽에서 타입이 문자열로 떨어진다.
                Assert.DoesNotContain("NaN", lines[1]);
            }
        }

        [Fact(DisplayName = "CSV — 쓸 수 없는 경로여도 예외를 던지지 않고 onError 로 보고한다")]
        public void FrameCsv_IoFailure_ReportsInsteadOfThrowing()
        {
            using (TempDir dir = new TempDir())
            {
                List<string> errors = new List<string>();
                object sync = new object();
                Action<string> onError = delegate(string m) { lock (sync) { errors.Add(m); } };

                Harness h = RunSample();

                // 예외가 밖으로 새면 워커 스레드가 죽고 앱이 멈춘다.
                FrameCsvLogger log = new FrameCsvLogger(SettingsFor(dir.BlockedSubPath()), onError);
                for (int i = 0; i < h.Snapshots.Count; i++) log.Log(h.Snapshots[i]);
                log.Flush();
                log.Dispose();

                lock (sync)
                {
                    Assert.NotEmpty(errors);
                }
                Assert.Equal(0, log.WrittenLines);
            }
        }

        [Fact(DisplayName = "CSV — EnableFrameCsv = false 면 파일을 만들지 않는다")]
        public void FrameCsv_Disabled_WritesNothing()
        {
            using (TempDir dir = new TempDir())
            {
                AppSettings s = SettingsFor(dir.Path);
                s.EnableFrameCsv = false;

                Harness h = RunSample();
                string path;
                using (FrameCsvLogger log = new FrameCsvLogger(s, null))
                {
                    Assert.False(log.Enabled);
                    for (int i = 0; i < h.Snapshots.Count; i++) log.Log(h.Snapshots[i]);
                    log.Flush();
                    path = log.FilePath;
                }

                Assert.False(File.Exists(path));
            }
        }

        [Fact(DisplayName = "session 요약 — 헤더와 데이터 행의 컬럼 수가 일치한다")]
        public void SessionSummary_ColumnCountsMatch()
        {
            using (TempDir dir = new TempDir())
            {
                Harness h = RunSample();
                using (FrameCsvLogger log = new FrameCsvLogger(SettingsFor(dir.Path), null))
                {
                    string path = log.WriteSessionSummary(h.Stats, h.Last);
                    Assert.NotNull(path);
                    Assert.True(File.Exists(path));

                    string[] lines = File.ReadAllLines(path);
                    Assert.Equal(2, lines.Length);
                    Assert.Equal(Csv.SplitLine(lines[0]).Count, Csv.SplitLine(lines[1]).Count);

                    IList<string> header = Csv.SplitLine(lines[0]);
                    IList<string> row = Csv.SplitLine(lines[1]);
                    Assert.Equal("S-01", row[header.IndexOf("seatId")]);
                    Assert.Equal("1", row[header.IndexOf("drowsyCount")]);
                }
            }
        }

        // ==================================================================
        // AlertJsonlLogger
        // ==================================================================

        [Fact(DisplayName = "JSONL — 알림 1건당 파싱 가능한 1줄이 기록된다")]
        public void AlertJsonl_WritesOneParseableLinePerAlert()
        {
            using (TempDir dir = new TempDir())
            {
                Harness h = RunSample();
                Assert.NotEmpty(h.Alerts);

                AlertJsonlLogger log = new AlertJsonlLogger(SettingsFor(dir.Path), null);
                for (int i = 0; i < h.Alerts.Count; i++) log.Write(h.Alerts[i]);

                Assert.Equal(h.Alerts.Count, log.WrittenCount);
                Assert.Equal(0, log.FailedCount);

                string[] lines = File.ReadAllLines(log.CurrentFilePath);
                Assert.Equal(h.Alerts.Count, lines.Length);
                for (int i = 0; i < lines.Length; i++)
                {
                    using (JsonDocument doc = JsonDocument.Parse(lines[i]))
                    {
                        Assert.Equal("1.0", doc.RootElement.GetProperty("schemaVersion").GetString());
                    }
                }
            }
        }

        [Fact(DisplayName = "JSONL — 쓸 수 없는 경로여도 예외를 던지지 않고 onError 로 보고한다")]
        public void AlertJsonl_IoFailure_ReportsInsteadOfThrowing()
        {
            using (TempDir dir = new TempDir())
            {
                List<string> errors = new List<string>();
                Action<string> onError = errors.Add;

                Harness h = RunSample();
                AlertJsonlLogger log = new AlertJsonlLogger(SettingsFor(dir.BlockedSubPath()), onError);
                log.Write(h.Alerts[0]);

                Assert.NotEmpty(errors);
                Assert.Equal(0, log.WrittenCount);
                Assert.Equal(1, log.FailedCount);
            }
        }

        // ==================================================================
        // AlertDispatcher
        // ==================================================================

        [Fact(DisplayName = "Dispatcher — 모든 sink 로 fan-out 하고, 한 sink 가 던져도 나머지는 계속 받는다")]
        public void Dispatcher_FanOut_IsolatesFailingSink()
        {
            List<string> errors = new List<string>();
            AlertDispatcher d = new AlertDispatcher(errors.Add);

            int a = 0, b = 0;
            d.AddSink("first", delegate { a++; });
            d.AddSink("boom", delegate { throw new InvalidOperationException("의도적 실패"); });
            d.AddUiSink(delegate { b++; });

            Harness h = RunSample();
            d.Dispatch(h.Alerts);

            Assert.Equal(h.Alerts.Count, a);
            Assert.Equal(h.Alerts.Count, b);
            Assert.Equal(h.Alerts.Count, d.DispatchedCount);
            Assert.Equal(h.Alerts.Count, errors.Count);
            Assert.Contains("boom", errors[0]);
        }

        [Fact(DisplayName = "Dispatcher — null 이벤트/목록은 조용히 무시한다")]
        public void Dispatcher_NullInputs_AreIgnored()
        {
            AlertDispatcher d = new AlertDispatcher();
            int calls = 0;
            d.AddSink("s", delegate { calls++; });

            d.Dispatch((AlertEvent)null);
            d.Dispatch((IList<AlertEvent>)null);
            d.AddSink("null-handler", null);

            Assert.Equal(0, calls);
            Assert.Equal(0, d.DispatchedCount);
            Assert.Equal(1, d.SinkCount);
        }
    }
}
