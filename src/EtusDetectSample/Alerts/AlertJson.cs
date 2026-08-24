using System;
using System.Globalization;
using System.Text;

namespace Etus.DetectSample.Alerts
{
    /// <summary>
    /// <see cref="AlertEvent"/> → JSON 직렬화. <b>외부 라이브러리 없이 직접 작성</b>한다
    /// (PoC 산출물의 의존성을 늘리지 않기 위해. Newtonsoft/System.Text.Json 모두 쓰지 않는다).
    ///
    /// 규칙
    ///  - <see cref="ToJson"/> 은 개행 없는 <b>한 줄</b>(JSONL 용).
    ///  - <see cref="ToPrettyJson"/> 은 UI 팝업/문서용 들여쓰기 버전. 내용은 동일하다.
    ///  - NaN / Infinity 는 유효한 JSON 이 아니므로 <c>null</c> 로 내보낸다.
    ///  - 숫자는 항상 InvariantCulture (한국어 로케일에서 소수점이 ','가 되는 사고 방지).
    ///  - 문자열은 ", \, 제어문자만 이스케이프하고 한글 등 non-ASCII 는 그대로 둔다(UTF-8 출력 전제).
    /// </summary>
    public static class AlertJson
    {
        /// <summary>초 단위 값의 소수 자릿수.</summary>
        private const int SecDigits = 2;
        /// <summary>0.0~1.0 비율/신뢰도의 소수 자릿수.</summary>
        private const int RatioDigits = 3;
        /// <summary>각도(도)의 소수 자릿수.</summary>
        private const int DegDigits = 2;

        /// <summary>ISO 8601 + 오프셋. 예: 2026-08-21T14:02:11.482+09:00</summary>
        private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fffzzz";

        /// <summary>JSONL 한 줄용. 개행을 포함하지 않는다.</summary>
        public static string ToJson(AlertEvent e)
        {
            return Build(e, false);
        }

        /// <summary>사람이 읽는 용도(UI 팝업/문서). 내용은 ToJson 과 동일하다.</summary>
        public static string ToPrettyJson(AlertEvent e)
        {
            return Build(e, true);
        }

        private static string Build(AlertEvent e, bool pretty)
        {
            if (e == null) return "null";

            StringBuilder sb = new StringBuilder(1024);
            sb.Append('{');
            if (pretty) sb.Append('\n');

            Prop(sb, pretty, 1, "schemaVersion", Str(AlertEvent.SchemaVersion), false);
            Prop(sb, pretty, 1, "eventId", Str(e.EventId), false);
            Prop(sb, pretty, 1, "occurredAt", Str(e.OccurredAt.ToString(TimeFormat, CultureInfo.InvariantCulture)), false);
            Prop(sb, pretty, 1, "seatId", Str(e.SeatId), false);
            Prop(sb, pretty, 1, "eventType", Str(TypeName(e.Type)), false);
            Prop(sb, pretty, 1, "level", Str(LevelName(e.Level)), false);
            Prop(sb, pretty, 1, "message", Str(e.Message), false);
            Prop(sb, pretty, 1, "durationSec", Num(e.DurationSec, SecDigits), false);

            // --- evidence ---
            if (e.Evidence == null)
            {
                Prop(sb, pretty, 1, "evidence", "null", false);
            }
            else
            {
                AlertEvidence ev = e.Evidence;
                NestedOpen(sb, pretty, 1, "evidence");
                Prop(sb, pretty, 2, "eyelidLeft", Num(ev.EyelidLeft, SecDigits), false);
                Prop(sb, pretty, 2, "eyelidRight", Num(ev.EyelidRight, SecDigits), false);
                Prop(sb, pretty, 2, "closedEyeSec", Num(ev.ClosedEyeSec, SecDigits), false);
                Prop(sb, pretty, 2, "perclos", Num(ev.Perclos, RatioDigits), false);
                Prop(sb, pretty, 2, "landmarkConfidence", Num(ev.LandmarkConfidence, RatioDigits), false);

                NestedOpen(sb, pretty, 2, "occlusion");
                Prop(sb, pretty, 3, "left", Num(ev.OcclusionLeft, RatioDigits), false);
                Prop(sb, pretty, 3, "right", Num(ev.OcclusionRight, RatioDigits), false);
                Prop(sb, pretty, 3, "mouth", Num(ev.OcclusionMouth, RatioDigits), true);
                NestedClose(sb, pretty, 2, false);

                Prop(sb, pretty, 2, "fineOcclusion", Num(ev.FineOcclusion, RatioDigits), false);
                Prop(sb, pretty, 2, "mask", Bool(ev.Mask), false);

                NestedOpen(sb, pretty, 2, "pose");
                Prop(sb, pretty, 3, "yaw", Num(ev.Yaw, DegDigits), false);
                Prop(sb, pretty, 3, "pitch", Num(ev.Pitch, DegDigits), false);
                Prop(sb, pretty, 3, "roll", Num(ev.Roll, DegDigits), true);
                NestedClose(sb, pretty, 2, false);

                Prop(sb, pretty, 2, "faceTrackId", Int(ev.FaceTrackId), false);
                Prop(sb, pretty, 2, "slumpSuspected", Bool(ev.SlumpSuspected), false);
                Prop(sb, pretty, 2, "unknownReason", Str(ev.UnknownReason), true);
                NestedClose(sb, pretty, 1, false);
            }

            // --- session ---
            if (e.Session == null)
            {
                Prop(sb, pretty, 1, "session", "null", true);
            }
            else
            {
                AlertSessionSummary s = e.Session;
                NestedOpen(sb, pretty, 1, "session");
                Prop(sb, pretty, 2, "seatedSec", Num(s.SeatedSec, SecDigits), false);
                Prop(sb, pretty, 2, "studySec", Num(s.StudySec, SecDigits), false);
                Prop(sb, pretty, 2, "drowsySec", Num(s.DrowsySec, SecDigits), false);
                Prop(sb, pretty, 2, "awaySec", Num(s.AwaySec, SecDigits), false);
                Prop(sb, pretty, 2, "unknownSec", Num(s.UnknownSec, SecDigits), false);
                Prop(sb, pretty, 2, "drowsyCount", Int(s.DrowsyCount), false);
                Prop(sb, pretty, 2, "awayCount", Int(s.AwayCount), false);
                Prop(sb, pretty, 2, "blinkCount", Int(s.BlinkCount), true);
                NestedClose(sb, pretty, 1, true);
            }

            sb.Append('}');
            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // enum → 대문자 문자열 (계약 스키마)
        // ------------------------------------------------------------------

        public static string TypeName(AlertType t)
        {
            switch (t)
            {
                case AlertType.Drowsy: return "DROWSY";
                case AlertType.Away: return "AWAY";
                case AlertType.Recovered: return "RECOVERED";
                default: return t.ToString().ToUpperInvariant();
            }
        }

        public static string LevelName(AlertLevel l)
        {
            switch (l)
            {
                case AlertLevel.Warn: return "WARN";
                case AlertLevel.Alert: return "ALERT";
                case AlertLevel.Clear: return "CLEAR";
                default: return l.ToString().ToUpperInvariant();
            }
        }

        // ------------------------------------------------------------------
        // 값 포맷
        // ------------------------------------------------------------------

        /// <summary>NaN / Infinity 는 JSON 에 표현할 수 없으므로 null 로 내보낸다.</summary>
        private static string Num(double v, int digits)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "null";
            return v.ToString("F" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        private static string Num(float v, int digits)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return "null";
            return ((double)v).ToString("F" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        private static string Int(int v)
        {
            return v.ToString(CultureInfo.InvariantCulture);
        }

        private static string Bool(bool v)
        {
            return v ? "true" : "false";
        }

        /// <summary>null 이면 JSON null 리터럴, 아니면 이스케이프된 문자열.</summary>
        private static string Str(string s)
        {
            if (s == null) return "null";
            StringBuilder sb = new StringBuilder(s.Length + 8);
            AppendEscaped(sb, s);
            return sb.ToString();
        }

        private static void AppendEscaped(StringBuilder sb, string s)
        {
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            // 그 외 제어문자는 \uXXXX
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            // 한글 등 non-ASCII 는 그대로 둔다 (파일은 UTF-8 로 기록한다)
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }

        // ------------------------------------------------------------------
        // 구조 작성 헬퍼
        // ------------------------------------------------------------------

        private static void Indent(StringBuilder sb, bool pretty, int depth)
        {
            if (!pretty) return;
            for (int i = 0; i < depth; i++) sb.Append("  ");
        }

        private static void Prop(StringBuilder sb, bool pretty, int depth, string name, string rawValue, bool last)
        {
            Indent(sb, pretty, depth);
            AppendEscaped(sb, name);
            sb.Append(':');
            if (pretty) sb.Append(' ');
            sb.Append(rawValue);
            if (!last) sb.Append(',');
            if (pretty) sb.Append('\n');
        }

        private static void NestedOpen(StringBuilder sb, bool pretty, int depth, string name)
        {
            Indent(sb, pretty, depth);
            AppendEscaped(sb, name);
            sb.Append(':');
            if (pretty) sb.Append(' ');
            sb.Append('{');
            if (pretty) sb.Append('\n');
        }

        private static void NestedClose(StringBuilder sb, bool pretty, int depth, bool last)
        {
            Indent(sb, pretty, depth);
            sb.Append('}');
            if (!last) sb.Append(',');
            if (pretty) sb.Append('\n');
        }
    }
}
