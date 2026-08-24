using System;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using Etus.DetectSample.Alerts;
using Etus.DetectSample.Analysis;
using Etus.DetectSample.Config;
using Etus.DetectSample.Tests.Support;
using Xunit;

namespace Etus.DetectSample.Tests
{
    /// <summary>
    /// 인포데스크 연동 payload.
    /// System.Text.Json 은 <b>테스트에서만</b> 쓴다(앱은 외부 의존 없이 직접 직렬화한다).
    /// 눈으로 보는 대신 실제 파서에 통과시키는 것이 목적이다.
    /// </summary>
    public class AlertJsonTests
    {
        /// <summary>실제 상태머신이 만든 알림 1건(직접 조립한 객체가 아니라 실제 경로 산출물).</summary>
        private static AlertEvent RealDrowsyAlert()
        {
            Harness h = new Harness();
            h.Run(Seq.Start().Open(1.0).Closed(5.0));
            return h.AlertsOf(AlertType.Drowsy, AlertLevel.Alert)[0];
        }

        [Fact(DisplayName = "생성된 JSON 이 실제로 파싱된다 (compact / pretty 둘 다)")]
        public void GeneratedJson_IsParseable()
        {
            AlertEvent e = RealDrowsyAlert();

            string compact = AlertJson.ToJson(e);
            Assert.DoesNotContain("\n", compact);   // JSONL 한 줄 계약
            using (JsonDocument doc = JsonDocument.Parse(compact))
            {
                JsonElement root = doc.RootElement;
                Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
                Assert.Equal("학생이 졸고 있습니다", root.GetProperty("message").GetString());
                Assert.Equal("S-01", root.GetProperty("seatId").GetString());
                Assert.True(root.GetProperty("durationSec").GetDouble() >= 5.0);
                Assert.True(root.GetProperty("evidence").GetProperty("closedEyeSec").GetDouble() >= 5.0);
                Assert.Equal(1, root.GetProperty("session").GetProperty("drowsyCount").GetInt32());
            }

            using (JsonDocument doc = JsonDocument.Parse(AlertJson.ToPrettyJson(e)))
            {
                Assert.Equal("DROWSY", doc.RootElement.GetProperty("eventType").GetString());
            }
        }

        [Fact(DisplayName = "eventType / level 은 대문자 계약 문자열이다")]
        public void TypeAndLevel_AreUpperCase()
        {
            Assert.Equal("DROWSY", AlertJson.TypeName(AlertType.Drowsy));
            Assert.Equal("AWAY", AlertJson.TypeName(AlertType.Away));
            Assert.Equal("RECOVERED", AlertJson.TypeName(AlertType.Recovered));
            Assert.Equal("WARN", AlertJson.LevelName(AlertLevel.Warn));
            Assert.Equal("ALERT", AlertJson.LevelName(AlertLevel.Alert));
            Assert.Equal("CLEAR", AlertJson.LevelName(AlertLevel.Clear));

            Harness h = new Harness();
            h.Run(Seq.Start().Open(1.0).NoFace(5.0));
            using (JsonDocument doc = JsonDocument.Parse(
                       AlertJson.ToJson(h.AlertsOf(AlertType.Away, AlertLevel.Alert)[0])))
            {
                Assert.Equal("AWAY", doc.RootElement.GetProperty("eventType").GetString());
                Assert.Equal("ALERT", doc.RootElement.GetProperty("level").GetString());
            }
        }

        [Fact(DisplayName = "NaN / Infinity 는 null 로 나간다 (JSON 스펙 위반 방지)")]
        public void NaNAndInfinity_BecomeNull()
        {
            // 속성 미측정 프레임(NaN)에서 알림이 나가는 상황을 그대로 만든다.
            // NaN 이 그대로 찍히면 인포데스크 파서가 죽는다.
            AlertEvent e = new AlertEvent();
            e.EventId = "abc";
            e.OccurredAt = new DateTimeOffset(2026, 8, 21, 14, 2, 11, 482, TimeSpan.FromHours(9));
            e.SeatId = "S-01";
            e.Type = AlertType.Drowsy;
            e.Level = AlertLevel.Alert;
            e.Message = "테스트";
            e.DurationSec = double.NaN;

            AlertEvidence ev = new AlertEvidence();
            ev.EyelidLeft = float.NaN;
            ev.EyelidRight = float.PositiveInfinity;
            ev.ClosedEyeSec = double.PositiveInfinity;
            ev.Perclos = double.NegativeInfinity;
            ev.LandmarkConfidence = float.NaN;
            ev.OcclusionLeft = float.NaN;
            ev.OcclusionRight = float.NaN;
            ev.OcclusionMouth = float.NaN;
            ev.FineOcclusion = float.NaN;
            ev.Yaw = float.NaN;
            ev.Pitch = float.NaN;
            ev.Roll = float.NaN;
            ev.FaceTrackId = -1;
            ev.UnknownReason = null;
            e.Evidence = ev;
            e.Session = new AlertSessionSummary();

            string json = AlertJson.ToJson(e);
            Assert.DoesNotContain("NaN", json);
            Assert.DoesNotContain("Infinity", json);

            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                JsonElement root = doc.RootElement;
                Assert.Equal(JsonValueKind.Null, root.GetProperty("durationSec").ValueKind);
                JsonElement evd = root.GetProperty("evidence");
                Assert.Equal(JsonValueKind.Null, evd.GetProperty("eyelidLeft").ValueKind);
                Assert.Equal(JsonValueKind.Null, evd.GetProperty("eyelidRight").ValueKind);
                Assert.Equal(JsonValueKind.Null, evd.GetProperty("closedEyeSec").ValueKind);
                Assert.Equal(JsonValueKind.Null, evd.GetProperty("perclos").ValueKind);
                Assert.Equal(JsonValueKind.Null, evd.GetProperty("occlusion").GetProperty("left").ValueKind);
                Assert.Equal(JsonValueKind.Null, evd.GetProperty("pose").GetProperty("yaw").ValueKind);
                Assert.Equal(JsonValueKind.Null, evd.GetProperty("unknownReason").ValueKind);
            }
        }

        [Fact(DisplayName = "문자열 이스케이프 — 따옴표/역슬래시/개행은 이스케이프, 한글은 그대로")]
        public void StringEscaping_IsCorrect()
        {
            AlertEvent e = RealDrowsyAlert();
            e.Message = "따옴표 \" 역슬래시 \\ 개행 \n 탭 \t 끝";
            e.SeatId = "S-01\r\n주입시도";

            string json = AlertJson.ToJson(e);
            Assert.DoesNotContain("\n", json);   // 한 줄 계약이 깨지면 JSONL 이 망가진다

            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                Assert.Equal("따옴표 \" 역슬래시 \\ 개행 \n 탭 \t 끝",
                             doc.RootElement.GetProperty("message").GetString());
                Assert.Equal("S-01\r\n주입시도", doc.RootElement.GetProperty("seatId").GetString());
            }

            // 한글은 \uXXXX 로 바꾸지 않고 그대로 둔다(UTF-8 출력 전제).
            Assert.Contains("따옴표", json);
        }

        [Fact(DisplayName = "숫자는 로케일과 무관하게 InvariantCulture 로 나간다")]
        public void Numbers_UseInvariantCulture()
        {
            // 한국어 로케일은 소수점이 '.' 이라 이 버그를 못 잡는다.
            // 소수점이 ',' 인 로케일(de-DE)로 바꿔야 실제로 검증된다.
            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

                AlertEvent e = RealDrowsyAlert();
                string json = AlertJson.ToJson(e);

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    // 파싱만 되면 소수점은 '.' 이다 ("5,00" 은 JSON 숫자가 아니라 파싱 자체가 실패한다).
                    Assert.True(doc.RootElement.GetProperty("durationSec").GetDouble() >= 5.0);
                }
                Assert.Contains("\"durationSec\":5.", json);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [Fact(DisplayName = "evidence / session 이 null 이어도 유효한 JSON")]
        public void NullSections_StillValidJson()
        {
            AlertEvent e = new AlertEvent();
            e.EventId = "x";
            e.SeatId = null;
            e.Message = null;
            e.Type = AlertType.Recovered;
            e.Level = AlertLevel.Clear;
            e.Evidence = null;
            e.Session = null;

            using (JsonDocument doc = JsonDocument.Parse(AlertJson.ToJson(e)))
            {
                Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("evidence").ValueKind);
                Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("session").ValueKind);
                Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("seatId").ValueKind);
            }

            Assert.Equal("null", AlertJson.ToJson(null));
        }

        [Fact(DisplayName = "occurredAt 은 ISO 8601 + 오프셋 형식이다")]
        public void OccurredAt_IsIso8601WithOffset()
        {
            AlertEvent e = new AlertEvent();
            e.OccurredAt = new DateTimeOffset(2026, 8, 21, 14, 2, 11, 482, TimeSpan.FromHours(9));

            using (JsonDocument doc = JsonDocument.Parse(AlertJson.ToJson(e)))
            {
                Assert.Equal("2026-08-21T14:02:11.482+09:00",
                             doc.RootElement.GetProperty("occurredAt").GetString());
            }
        }
    }
}
