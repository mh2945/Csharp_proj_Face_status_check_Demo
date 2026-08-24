using System;

namespace Etoos.DetectSample.Alerts
{
    /// <summary>알림 종류.</summary>
    public enum AlertType
    {
        Drowsy = 0,
        Away,
        Recovered,
        PersonChanged,
    }

    /// <summary>알림 수준. 의심=Warn, 확정=Alert, 복귀=Clear.</summary>
    public enum AlertLevel
    {
        Warn = 0,
        Alert,
        Clear,
    }

    /// <summary>
    /// 중앙 인포데스크로 넘길 수 있는 알림 payload.
    /// FASMH-94 의 "실시간 알림으로 사용할 인자값 제공이 가능한지 보여줘야 함" 에 대한 답.
    /// alerts-YYYYMMDD.jsonl 에 1줄씩 기록되고 UI Alert 목록에 동일 내용이 노출된다.
    /// </summary>
    public sealed class AlertEvent
    {
        public const string SchemaVersion = "1.0";

        public string EventId;          // GUID "N" 포맷
        public DateTimeOffset OccurredAt;
        public string SeatId;           // PoC 는 단일 좌석 "S-01"
        public AlertType Type;
        public AlertLevel Level;
        public string Message;          // "학생이 졸고 있습니다" 등 한국어 문구
        public double DurationSec;      // 판정 근거가 된 지속 시간

        public AlertEvidence Evidence;
        public AlertSessionSummary Session;
    }

    /// <summary>판정 근거가 된 SDK 원시값 묶음. 고객이 "무엇을 보고 판단했나"를 확인하는 부분.</summary>
    public sealed class AlertEvidence
    {
        public float EyelidLeft, EyelidRight;
        public double ClosedEyeSec;
        public double Perclos;
        public float LandmarkConfidence;
        public float OcclusionLeft, OcclusionRight, OcclusionMouth;
        public float FineOcclusion;
        public bool Mask;
        public float Yaw, Pitch, Roll;
        public int FaceTrackId;
        public bool SlumpSuspected;
        public string UnknownReason;    // enum 이름 또는 null
    }

    /// <summary>알림 시점의 누적 집계. 좌석별 착석/공부시간 산출 가능성을 증명한다.</summary>
    public sealed class AlertSessionSummary
    {
        public double SeatedSec, StudySec, DrowsySec, AwaySec, UnknownSec;
        public int DrowsyCount, AwayCount;
    }
}
