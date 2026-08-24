using System;
using System.Collections.Generic;
using Etus.DetectSample.Analysis;

namespace Etus.DetectSample.Tests.Support
{
    /// <summary>
    /// 합성 프레임 시퀀스 빌더.
    ///
    /// <code>
    /// // 눈 뜬 상태 1초 → 눈 감은 상태 5초 → 다시 뜬 상태 2초
    /// var frames = Seq.Start().Open(1.0).Closed(5.0).Open(2.0).Build();
    /// </code>
    ///
    /// [왜 기본 8 FPS 인가]
    /// dt = 1/8 = 0.125 는 2의 거듭제곱이라 double 로 <b>정확히</b> 표현된다.
    /// 임계값 2.0 / 5.0 / 1.5 / 0.5 가 전부 0.125 의 배수라 경계 테스트에서
    /// 부동소수 오차로 결과가 흔들리지 않는다. 목표 분석 레이트(8~10 FPS)와도 일치한다.
    /// 10 FPS(dt=0.1)는 double 로 정확히 표현되지 않아 경계값 테스트가 불안정해진다.
    ///
    /// [시간축] Thread.Sleep 을 쓰지 않는다. MonotonicSec 만 증가시킨다.
    ///
    /// [프레임과 누적값의 관계 — 테스트 기대값 계산법]
    /// 첫 프레임은 dt=0 이라 아무것도 적립하지 않는다. 그래서
    /// <c>Open(1.0).Closed(5.0)</c> 의 마지막 프레임에서 ClosedEyeSec 은 정확히 5.0 이 된다
    /// (감김 40 프레임 x 0.125). <c>Closed(4.875)</c> 는 39 프레임이라 최대 4.875 로,
    /// 5.0 임계를 넘기지 못하는 바로 직전 표본이다.
    /// </summary>
    public sealed class Seq
    {
        /// <summary>기본 분석 레이트. 목표 FPS 8 이상(계획 문서) 과 일치시킨다.</summary>
        public const double DefaultFps = 8.0;

        private readonly double _dt;
        private readonly List<FrameObservation> _frames = new List<FrameObservation>();
        private readonly DateTime _wallBase = new DateTime(2026, 8, 21, 14, 0, 0);

        private double _next;     // 다음 프레임의 MonotonicSec
        private long _index;
        private int _trackId = 1;

        private Seq(double fps)
        {
            if (fps <= 0.0) throw new ArgumentOutOfRangeException("fps");
            _dt = 1.0 / fps;
        }

        public static Seq Start()
        {
            return new Seq(DefaultFps);
        }

        public static Seq Start(double fps)
        {
            return new Seq(fps);
        }

        /// <summary>프레임 간격(초).</summary>
        public double Dt { get { return _dt; } }

        /// <summary>지금까지 만들어진 프레임 수. 시퀀스 중간 지점을 표시할 때 쓴다.</summary>
        public int Count { get { return _frames.Count; } }

        /// <summary>다음 프레임이 놓일 시각(초).</summary>
        public double NextSec { get { return _next; } }

        public IList<FrameObservation> Build()
        {
            return _frames;
        }

        // ------------------------------------------------------------------
        // 시퀀스 구성
        // ------------------------------------------------------------------

        /// <summary>이후 프레임의 Face.id 를 바꾼다. 얼굴 미검출 프레임은 항상 -1 이다.</summary>
        public Seq TrackId(int id)
        {
            _trackId = id;
            return this;
        }

        /// <summary>눈 뜬 상태. eyelid 3.8 / 3.9 (임계 2.4 초과).</summary>
        public Seq Open(double sec)
        {
            return Emit(FramesFor(sec), AsOpen);
        }

        /// <summary>눈 감은 상태. eyelid 1.8 / 1.9 (임계 2.4 이하).</summary>
        public Seq Closed(double sec)
        {
            return Emit(FramesFor(sec), AsClosed);
        }

        /// <summary>얼굴 미검출. FaceDetected=false, Face.id=-1, 속성값 전부 NaN.</summary>
        public Seq NoFace(double sec)
        {
            return Emit(FramesFor(sec), AsNoFace);
        }

        /// <summary>
        /// 지정한 사유로 EyeStateGate 가 Unknown 을 내도록 만든 프레임.
        /// 게이트를 우회하지 않고 <b>실제 게이트가 그 사유를 내도록</b> 원시값을 조작한다.
        /// </summary>
        public Seq Unknown(UnknownReason reason, double sec)
        {
            UnknownReason r = reason;
            return Emit(FramesFor(sec), delegate(FrameObservation o) { ApplyUnknown(o, r); });
        }

        /// <summary>프레임 유실. 프레임을 만들지 않고 시각만 건너뛴다.</summary>
        public Seq Gap(double sec)
        {
            _next += sec;
            return this;
        }

        /// <summary>프레임 수로 직접 지정(경계 테스트용).</summary>
        public Seq OpenFrames(int count)
        {
            return Emit(count, AsOpen);
        }

        /// <summary>프레임 수로 직접 지정(경계 테스트용).</summary>
        public Seq ClosedFrames(int count)
        {
            return Emit(count, AsClosed);
        }

        /// <summary>임의 조작. 위 조합으로 안 되는 프레임을 만들 때만 쓴다.</summary>
        public Seq Custom(double sec, Action<FrameObservation> fill)
        {
            return Emit(FramesFor(sec), fill);
        }

        /// <summary>초 → 프레임 수. 0.125 배수를 넣으면 반올림 오차가 없다.</summary>
        public int FramesFor(double sec)
        {
            return (int)Math.Round(sec / _dt, MidpointRounding.AwayFromZero);
        }

        // ------------------------------------------------------------------
        // 프레임 생성
        // ------------------------------------------------------------------

        private Seq Emit(int count, Action<FrameObservation> fill)
        {
            for (int i = 0; i < count; i++)
            {
                FrameObservation o = NewBase();
                if (fill != null) fill(o);
                _frames.Add(o);
                _next += _dt;
                _index++;
            }
            return this;
        }

        /// <summary>정상 검출 + 모든 속성 측정됨 + 눈 뜬 상태를 기본값으로 한다.</summary>
        private FrameObservation NewBase()
        {
            FrameObservation o = new FrameObservation();
            o.MonotonicSec = _next;
            o.WallClock = _wallBase.AddSeconds(_next);
            o.FrameIndex = _index;
            o.SdkError = null;

            o.FaceDetected = true;
            o.FaceCount = 1;
            o.FaceTrackId = _trackId;

            o.BoxX = 240f; o.BoxY = 420f; o.BoxW = 240f; o.BoxH = 300f;
            o.Yaw = -2.0f; o.Pitch = 4.0f; o.Roll = 1.0f;

            o.LandmarkConfidence = 0.97f;
            o.MaskConfidence = 0.88f;
            o.IsMasked = false;

            o.OcclusionLeftEye = 0.02f;
            o.OcclusionRightEye = 0.03f;
            o.OcclusionMouth = 0.01f;
            o.FineOcclusion = 0.04f;
            o.FineOcclusionAvailable = true;

            o.EyelidLeft = 3.8f;
            o.EyelidRight = 3.9f;
            o.EyelidValid = true;

            o.SdkElapsedMs = 12.5;
            o.Landmark106 = null;
            return o;
        }

        private static void AsOpen(FrameObservation o)
        {
            // NewBase 가 이미 눈 뜬 상태다. 명시적으로 남겨 둔다.
            o.EyelidLeft = 3.8f;
            o.EyelidRight = 3.9f;
            o.EyelidValid = true;
        }

        private static void AsClosed(FrameObservation o)
        {
            o.EyelidLeft = 1.8f;
            o.EyelidRight = 1.9f;
            o.EyelidValid = true;
        }

        private static void AsNoFace(FrameObservation o)
        {
            o.FaceDetected = false;
            o.FaceCount = 0;
            o.FaceTrackId = -1;
            o.BoxX = 0f; o.BoxY = 0f; o.BoxW = 0f; o.BoxH = 0f;
            o.Yaw = float.NaN; o.Pitch = float.NaN; o.Roll = float.NaN;
            o.LandmarkConfidence = float.NaN;
            o.MaskConfidence = float.NaN;
            o.IsMasked = false;
            o.OcclusionLeftEye = float.NaN;
            o.OcclusionRightEye = float.NaN;
            o.OcclusionMouth = float.NaN;
            o.FineOcclusion = float.NaN;
            o.EyelidLeft = float.NaN;
            o.EyelidRight = float.NaN;
            o.EyelidValid = false;
        }

        /// <summary>사유별로 게이트에 걸릴 원시값을 심는다.</summary>
        internal static void ApplyUnknown(FrameObservation o, UnknownReason reason)
        {
            switch (reason)
            {
                case UnknownReason.SdkError:
                    o.SdkError = "DetectFace 실패(테스트)";
                    break;

                case UnknownReason.NoFace:
                    AsNoFace(o);
                    break;

                case UnknownReason.LowLandmarkConfidence:
                    o.LandmarkConfidence = 0.50f;   // < 0.90
                    break;

                case UnknownReason.PoseOutOfRange:
                    o.Pitch = 40.0f;                // > 25도 (고개 숙임)
                    break;

                case UnknownReason.EyeOccluded:
                    o.OcclusionLeftEye = 0.90f;     // > 0.50 (손으로 눈 가림)
                    break;

                case UnknownReason.FineOccluded:
                    o.FineOcclusion = 0.90f;        // > 0.50
                    o.FineOcclusionAvailable = true;
                    break;

                case UnknownReason.AsymmetricEye:
                    o.EyelidLeft = 1.8f;            // 왼쪽만 감김
                    o.EyelidRight = 3.9f;
                    o.EyelidValid = true;
                    break;

                case UnknownReason.EyelidUnavailable:
                    // DetectClosedEyes 가 IsOk() 가 아니었던 경우 — 값은 0 으로 남는다.
                    o.EyelidLeft = 0f;
                    o.EyelidRight = 0f;
                    o.EyelidValid = false;
                    break;

                default:
                    throw new ArgumentOutOfRangeException("reason", reason, "Unknown 을 만들 수 없는 사유");
            }
        }
    }
}
