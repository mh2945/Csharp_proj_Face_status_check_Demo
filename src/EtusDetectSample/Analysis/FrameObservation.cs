using System;

namespace Etoos.DetectSample.Analysis
{
    /// <summary>
    /// 프레임 1장에 대한 SDK 원시 출력. 판정은 하지 않는다.
    /// Sdk/FaceEngine 이 생산하고 Analysis/Alerts/Logging/UI 가 소비하는 공용 계약.
    /// 이 타입은 FaceSDK / OpenCvSharp / WinForms 를 참조하지 않는다.
    /// </summary>
    public sealed class FrameObservation
    {
        /// <summary>단조 증가 시각(초). Stopwatch 기반. DateTime 사용 금지.</summary>
        public double MonotonicSec;

        /// <summary>로그 표기용 벽시계. 판정에 사용하지 않는다.</summary>
        public DateTime WallClock;

        /// <summary>프레임 일련번호(0부터).</summary>
        public long FrameIndex;

        /// <summary>DetectFace 자체가 에러를 반환한 경우 사유. 정상이면 null.</summary>
        public string SdkError;

        /// <summary>얼굴 검출 여부 (face_cnt &gt; 0).</summary>
        public bool FaceDetected;

        /// <summary>프레임에서 검출된 얼굴 수. C# wrapper 는 box 를 1개만 주지만 개수는 알려준다.</summary>
        public int FaceCount;

        /// <summary>Face.id — continuous tracking ID. 미검출 시 -1.</summary>
        public int FaceTrackId;

        // --- face box (픽셀 좌표) ---
        public float BoxX, BoxY, BoxW, BoxH;

        // --- head pose (degree) ---
        public float Yaw, Pitch, Roll;

        /// <summary>ComputeLandmarkConfidence 결과 0.0~1.0. 미측정이면 float.NaN.</summary>
        public float LandmarkConfidence;

        /// <summary>
        /// CheckMask 의 face_mask_confidence 원시값. 미측정이면 float.NaN.
        /// 주의: 이 값이 <b>낮을수록</b> 마스크 착용이다. 직접 비교 금지.
        /// </summary>
        public float MaskConfidence;

        /// <summary>Rst.IsFaceMasked() 결과를 그대로 옮긴 값. 반전 로직을 재구현하지 말 것.</summary>
        public bool IsMasked;

        // --- DetectFaceOcclusion (0.0~1.0). 미측정이면 float.NaN. ---
        public float OcclusionLeftEye, OcclusionRightEye, OcclusionMouth;

        /// <summary>DetectFineOcclusion score. 미측정/비활성이면 float.NaN.</summary>
        public float FineOcclusion;

        /// <summary>FineOcclusion 이 런타임에 사용 가능한지. 모델 미포함 시 false 로 떨어진다.</summary>
        public bool FineOcclusionAvailable;

        // --- DetectClosedEyes 눈꺼풀 거리. 미측정이면 float.NaN. ---
        public float EyelidLeft, EyelidRight;

        /// <summary>DetectClosedEyes 가 IsOk() 였는지. false 면 Eyelid* 값은 신뢰 불가.</summary>
        public bool EyelidValid;

        /// <summary>이 프레임 처리에 걸린 SDK 호출 총 시간(ms). 성능 표시용.</summary>
        public double SdkElapsedMs;

        /// <summary>106-point landmark (x,y 쌍, 길이 212). 오버레이 전용. 없으면 null.</summary>
        public float[] Landmark106;
    }

    /// <summary>게이트를 통과한 뒤의 눈 상태.</summary>
    public enum EyeState
    {
        Unknown = 0,
        Open,
        Closed,
    }

    /// <summary>EyeState.Unknown 인 이유. UI/CSV 에 그대로 노출한다.</summary>
    public enum UnknownReason
    {
        None = 0,
        NoFace,
        SdkError,
        LowLandmarkConfidence,
        PoseOutOfRange,
        EyeOccluded,
        FineOccluded,
        AsymmetricEye,
        EyelidUnavailable,
    }

    /// <summary>학생 착석 여부.</summary>
    public enum Presence
    {
        Absent = 0,
        Present,
    }

    /// <summary>EyeStateGate 의 산출물.</summary>
    public struct EyeDecision
    {
        public EyeState State;
        public UnknownReason Reason;
        public Presence Presence;

        public EyeDecision(EyeState state, UnknownReason reason, Presence presence)
        {
            State = state;
            Reason = reason;
            Presence = presence;
        }
    }

    /// <summary>SeatStateMachine 이 노출하는 좌석 상태.</summary>
    public enum SeatState
    {
        Idle = 0,
        Awake,
        DrowsySuspect,
        Drowsy,
        AwaySuspect,
        Away,
        Unknown,
    }

    /// <summary>
    /// 한 프레임 처리 후 UI 가 그리는 데 필요한 전부.
    /// 워커 스레드가 만들어 BeginInvoke 로 UI 에 넘긴다(불변 취급).
    /// </summary>
    public sealed class SeatSnapshot
    {
        public FrameObservation Observation;
        public EyeDecision Eye;
        public SeatState State;

        /// <summary>엎드림 휴리스틱이 AWAY 대신 DROWSY 를 유지시킨 상태인지.</summary>
        public bool SlumpSuspected;

        // --- 실시간 신호 ---
        public double ClosedEyeSec;   // 연속 눈 감김 시간
        public double NoFaceSec;      // 연속 얼굴 미검출 시간
        public double Perclos;        // 0.0~1.0, sliding window
        public double StateElapsedSec;// 현재 상태 유지 시간

        // --- 누적 통계 ---
        public double SeatedSec, StudySec, DrowsySec, AwaySec, UnknownSec;
        public int DrowsyCount, AwayCount;

        /// <summary>측정 FPS.</summary>
        public double Fps;
    }
}
