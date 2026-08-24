using System;
using Etus.DetectSample.Config;

namespace Etus.DetectSample.Analysis
{
    /// <summary>
    /// 프레임 1장의 원시 관측값을 "믿을 수 있는 눈 상태"로 환산하는 게이트.
    /// 상태(시간축)를 전혀 갖지 않는 순수 함수다. 시간 판정은 <see cref="SeatStateMachine"/> 담당.
    ///
    /// 설계 의도: 조금이라도 신뢰할 수 없는 프레임은 Open/Closed 로 단정하지 않고
    /// Unknown + 사유를 돌려준다. 잘못된 Closed 하나가 5초 뒤 오알림이 되기 때문이다.
    /// </summary>
    public sealed class EyeStateGate
    {
        private readonly AppSettings _settings;

        public EyeStateGate(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            _settings = settings;
        }

        /// <summary>
        /// 판정 순서(먼저 걸리는 것이 이긴다):
        /// SdkError → NoFace → LandmarkConf → Pose → Occlusion → FineOcclusion → EyelidValid → 개폐.
        /// </summary>
        public EyeDecision Evaluate(FrameObservation obs)
        {
            if (obs == null)
                return new EyeDecision(EyeState.Unknown, UnknownReason.SdkError, Presence.Absent);

            // 1) SDK 자체 에러 — 값 필드는 채워지지 않았다(CONTRACT.md 1절). 아무것도 믿지 않는다.
            if (obs.SdkError != null)
                return new EyeDecision(EyeState.Unknown, UnknownReason.SdkError, Presence.Absent);

            // 2) 얼굴 미검출 → 부재. 이석 판정은 SeatStateMachine 의 NoFaceSec 이 담당한다.
            if (!obs.FaceDetected)
                return new EyeDecision(EyeState.Unknown, UnknownReason.NoFace, Presence.Absent);

            // 3) 여기부터는 얼굴이 있으므로 착석(Present)으로 본다.
            //    아래 게이트에 걸려도 Presence 는 Present 를 유지한다 — "앉아 있지만 눈을 못 본다"이지
            //    "자리에 없다"가 아니다. 이 구분이 SeatedSec 정확도를 좌우한다.

            // 4) landmark 신뢰도. NaN 은 "미측정"이므로 통과시킨다(아래 NaN 주석 참조).
            //    임계값을 float 로 낮춰서 비교한다 — 이유는 아래 [경계 비교] 주석 참조.
            if (!float.IsNaN(obs.LandmarkConfidence) && obs.LandmarkConfidence < (float)_settings.LandmarkConfMin)
                return new EyeDecision(EyeState.Unknown, UnknownReason.LowLandmarkConfidence, Presence.Present);

            // 5) 고개 각도. 범위를 벗어나면 눈꺼풀 거리 자체가 왜곡된다.
            //    Yaw/Pitch 가 NaN 이면 비교가 false 라 통과한다(미측정 → 판정 계속).
            if (Math.Abs(obs.Pitch) > (float)_settings.PosePitchMaxDeg ||
                Math.Abs(obs.Yaw) > (float)_settings.PoseYawMaxDeg)
                return new EyeDecision(EyeState.Unknown, UnknownReason.PoseOutOfRange, Presence.Present);

            // 6) 눈 가림(DetectFaceOcclusion). 부등호 방향은 IsOccluded() 한 곳에만 있다.
            if (IsOccluded(obs.OcclusionLeftEye, _settings.OcclusionMax) ||
                IsOccluded(obs.OcclusionRightEye, _settings.OcclusionMax))
                return new EyeDecision(EyeState.Unknown, UnknownReason.EyeOccluded, Presence.Present);

            // 7) 정밀 가림. 방향이 확인되어 UseFineOcclusionGate 기본값이 true 다(CONTRACT.md 부록 A 4번).
            //    그래도 조건은 3중으로 유지한다 — 설정 ON + 런타임 사용 가능 + 임계값 초과.
            //    model package 에 DetectFineOcclusion 모델이 들어 있는지는 런타임에만 알 수 있고,
            //    FaceEngine 이 FineOcclusionAvailable=false 로 내리면 설정과 무관하게 건너뛴다
            //    (CONTRACT.md 5절 — 모델 미포함으로 앱이 죽으면 안 된다).
            if (_settings.UseFineOcclusionGate && obs.FineOcclusionAvailable &&
                IsOccluded(obs.FineOcclusion, _settings.FineOcclusionMax))
                return new EyeDecision(EyeState.Unknown, UnknownReason.FineOccluded, Presence.Present);

            // 8) DetectClosedEyes 가 IsOk() 가 아니었다면 Eyelid* 는 0 으로 남아 있다.
            //    0 을 "눈 감김"으로 오독하지 않기 위한 필수 게이트(CONTRACT.md 1절).
            if (!obs.EyelidValid)
                return new EyeDecision(EyeState.Unknown, UnknownReason.EyelidUnavailable, Presence.Present);

            // 8-1) EyelidValid 인데 값이 NaN 인 경우(방어). 아래 비교가 전부 false 가 되어
            //      AsymmetricEye 로 잘못 흘러가는 것을 막는다.
            if (float.IsNaN(obs.EyelidLeft) || float.IsNaN(obs.EyelidRight))
                return new EyeDecision(EyeState.Unknown, UnknownReason.EyelidUnavailable, Presence.Present);

            // 9) 눈 개폐. 양쪽이 일치할 때만 단정한다.
            //
            // [경계 비교] SDK 값은 float, 임계값은 double 이다. float 를 double 로 넓혀서 비교하면
            // 2.4f 는 2.4000000953674316 이 되어 "eyelid <= 2.4 면 Closed" 라는 AppSettings 의
            // 문서와 경계에서 어긋난다(정확히 2.4 인 값이 Open 으로 판정된다).
            // 그래서 임계값 쪽을 float 로 낮춰서 같은 정밀도끼리 비교한다.
            // 위 4/5/6/7번 게이트도 같은 이유로 (float) 캐스팅을 한다.
            float th = (float)_settings.EyelidClosedThreshold;
            bool leftClosed = obs.EyelidLeft <= th;
            bool rightClosed = obs.EyelidRight <= th;

            if (leftClosed && rightClosed)
                return new EyeDecision(EyeState.Closed, UnknownReason.None, Presence.Present);
            if (!leftClosed && !rightClosed)
                return new EyeDecision(EyeState.Open, UnknownReason.None, Presence.Present);

            // 한쪽만 감김 — 윙크/부분 가림/랜드마크 흔들림. 졸음 근거로 쓰지 않는다.
            return new EyeDecision(EyeState.Unknown, UnknownReason.AsymmetricEye, Presence.Present);
        }

        /// <summary>
        /// occlusion 계열 점수의 <b>방향을 결정하는 유일한 지점</b>.
        ///
        /// [방향 — 확인됨] <b>가려질수록 값이 높아진다.</b> (담당자 확인, 2026-08-21 / CONTRACT.md 부록 A 3·4번)
        ///   - DetectFaceOcclusion(left/right_eye_occlusion_confidence): `score &gt; OcclusionMax` → 가려짐.
        ///   - DetectFineOcclusion: 위와 동일 방향. `score &gt; FineOcclusionMax` → 가려짐.
        ///   - Mask 만 반대다(face_mask_confidence 는 "낮을수록" 착용 — CONTRACT.md 1절).
        ///     그래서 Mask 는 이 게이트를 타지 않고 obs.IsMasked(= Rst.IsFaceMasked()) 를 그대로 쓴다.
        ///     Mask 가 반전이라고 해서 occlusion 까지 반전이라고 넘겨짚지 말 것 — 서로 다르다.
        ///   - <b>부등호를 뒤집지 말 것.</b> 위 방향은 추측이 아니라 확인된 사실이다.
        ///     그럼에도 재보정으로 방향을 바꿔야 할 일이 생기면 호출부는 그대로 두고 이 메서드만 고친다.
        ///
        /// [NaN 처리 — 의도된 동작]
        /// NaN 은 "가려지지 않음"이 아니라 <b>"미측정"</b>이다.
        /// NaN 비교는 항상 false 라 그대로 두면 자동 통과하는데, 여기서는 그것이 의도한 동작이다.
        /// 이유: CheckMask / DetectFaceOcclusion / DetectFineOcclusion 은
        /// AppSettings.AttrIntervalFrames(기본 5) 주기로만 호출되므로 대부분의 프레임에서 NaN 이다.
        /// NaN 을 Unknown 으로 처리하면 5프레임 중 4프레임이 Unknown 이 되어 시간 판정이 불가능해진다.
        /// 즉 "미측정 프레임은 가림 게이트를 건너뛴다"가 설계다. 가림은 측정된 프레임에서만 걸린다.
        /// </summary>
        private static bool IsOccluded(float score, double maxAllowed)
        {
            if (float.IsNaN(score)) return false;   // 미측정 → 게이트 통과 (위 주석 참조)
            return score > (float)maxAllowed;       // 경계 비교는 float 정밀도로 (9번 주석 참조)
        }
    }
}
