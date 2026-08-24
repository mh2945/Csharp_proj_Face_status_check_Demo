using System;
using System.Collections.Generic;
using System.Globalization;
using Etus.DetectSample.Alerts;
using Etus.DetectSample.Config;

namespace Etus.DetectSample.Analysis
{
    /// <summary>
    /// 프레임 단위 <see cref="EyeDecision"/> 을 시간축 좌석 상태로 바꾸는 상태머신.
    ///
    /// [설계 원칙]
    ///  - 시간은 전부 <see cref="FrameObservation.MonotonicSec"/> 로만 계산한다.
    ///    내부에서 DateTime / Stopwatch / Timer / Thread.Sleep 을 쓰지 않는다(단위 테스트 가능성).
    ///  - Push() 는 프레임 1장을 밀어넣는 유일한 입구다. 부작용은 stats 갱신과 내부 필드뿐이다.
    ///  - 스레드 안전하지 않다. 워커 스레드 1개가 소유한다(CONTRACT.md 3절).
    ///
    /// [시간 적립 규칙 — 테스트 작성 시 중요]
    ///  - 누적 통계(SessionStats)는 <b>구간 시작 시점의 상태</b>로 적립한다(left-endpoint).
    ///    즉 [t(n-1), t(n)] 구간은 t(n-1) 프레임의 상태/착석여부로 적립된다.
    ///  - 반대로 ClosedEyeSec / NoFaceSec 은 <b>현재 프레임의 판정</b>으로 적립한다(right-endpoint).
    ///    "t=0 부터 눈 감김, 100ms 간격"이면 t=2.0 프레임에서 ClosedEyeSec == 2.0 이 된다.
    /// </summary>
    public sealed class SeatStateMachine
    {
        // --- 알림 문구 (테스트에서 문자열 비교에 쓸 수 있도록 public const) ---
        public const string MsgDrowsySuspect = "학생이 졸고 있는 것 같습니다";
        public const string MsgDrowsyConfirmed = "학생이 졸고 있습니다";
        public const string MsgAwaySuspect = "학생이 자리를 비운 것 같습니다";
        public const string MsgAwayConfirmed = "학생이 좌석을 옮겼습니다";
        public const string MsgRecovered = "학생이 정상 상태로 돌아왔습니다";
        public const string MsgPerclos = "눈 감김 비율(PERCLOS)이 높습니다";

        /// <summary>
        /// 엎드림(slump) 유지 상한 배수.
        /// 상한 = AppSettings.NoFaceConfirmSec * SlumpAwayFallbackMultiplier.
        /// 기본 설정에서는 5.0 * 3.0 = 15초. 이 시간을 넘도록 얼굴이 안 보이면
        /// "엎드려 자는 중"이 아니라 "진짜 자리를 비웠다"로 본다.
        /// 절대 시간을 새 임계값으로 박지 않고 기존 임계값의 배수로 유도한다(매직 넘버 금지).
        /// </summary>
        private const double SlumpAwayFallbackMultiplier = 3.0;

        /// <summary>FPS 평활화용 이동평균 창(프레임 수). 판정 임계값이 아니라 표시용 상수다.</summary>
        private const int FpsWindowFrames = 30;

        private struct PerclosSample
        {
            public double AtSec;
            public double Dt;
            public bool Closed;
        }

        private readonly AppSettings _settings;
        private readonly Queue<PerclosSample> _perclosWindow = new Queue<PerclosSample>();
        private readonly Dictionary<int, double> _cooldownAt = new Dictionary<int, double>();
        private readonly double[] _fpsRing = new double[FpsWindowFrames];

        // --- 시간축 ---
        private bool _hasPrev;
        private double _prevMonotonicSec;
        private Presence _prevPresence = Presence.Absent;
        private EyeState _prevEyeState = EyeState.Unknown;

        // --- 상태 ---
        private SeatState _state = SeatState.Idle;
        private double _stateEnteredSec;

        // --- 신호 ---
        private double _closedEyeSec;
        private double _noFaceSec;
        private double _unknownSec;
        private double _observedSec;      // 리셋 이후 실제로 관측한 시간(PERCLOS 준비 판단용)
        private bool _faceLost;
        private bool _slumpSuspected;
        private bool _blinkCandidate;

        // --- PERCLOS 러닝 합 ---
        private double _perclosClosedSec;
        private double _perclosOpenSec;

        // --- 알림 ---
        private bool _pendingRecovery;    // 확정(Alert) 상태에 들어간 뒤 아직 복귀 알림을 안 낸 상태
        private double _abnormalSinceSec; // 그 확정 상태에 처음 들어간 시각

        // --- tracking / 리셋 ---
        private int _lastValidTrackId = -1;
        private string _lastResetReason;

        // --- FPS ---
        private int _fpsIndex;
        private int _fpsCount;
        private double _fpsSum;

        /// <summary>
        /// 리셋 사유 통보 콜백(선택). SeatSnapshot 에는 리셋 사유 필드가 없으므로
        /// 프레임 유실 / Face.id 변경 리셋을 로그로 남기려면 이 콜백을 연결한다.
        /// 콜백이 예외를 던져도 상태머신은 계속 동작한다.
        /// </summary>
        public Action<string> ResetLogger;

        public SeatStateMachine(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            _settings = settings;
        }

        public SeatState State { get { return _state; } }
        public double ClosedEyeSec { get { return _closedEyeSec; } }
        public double NoFaceSec { get { return _noFaceSec; } }
        public double UnknownSec { get { return _unknownSec; } }
        public bool SlumpSuspected { get { return _slumpSuspected; } }
        public double Perclos { get { return ComputePerclos(); } }
        /// <summary>마지막 리셋 사유. 리셋된 적이 없으면 null.</summary>
        public string LastResetReason { get { return _lastResetReason; } }
        /// <summary>엎드림 유지 상한(초). = NoFaceConfirmSec * 3.0</summary>
        public double SlumpAwayFallbackSec { get { return _settings.NoFaceConfirmSec * SlumpAwayFallbackMultiplier; } }

        /// <summary>프레임 1장을 밀어넣고, 이번 프레임에서 발생한 알림들을 돌려준다.</summary>
        public IList<AlertEvent> Push(FrameObservation obs, EyeDecision eye, SessionStats stats, out SeatSnapshot snapshot)
        {
            if (obs == null) throw new ArgumentNullException("obs");
            if (stats == null) throw new ArgumentNullException("stats");

            List<AlertEvent> alerts = new List<AlertEvent>();
            double now = obs.MonotonicSec;

            // ---------- 1) dt 계산 + 리셋 판정 ----------
            double dt = 0.0;
            string resetReason = null;

            if (_hasPrev)
            {
                dt = now - _prevMonotonicSec;
                if (double.IsNaN(dt) || double.IsInfinity(dt) || dt < 0.0)
                {
                    // Stopwatch 는 역행하지 않지만, 워커 재시작/버그로 들어올 수 있다. 방어.
                    resetReason = "MonotonicSec 역행/비정상";
                    dt = 0.0;
                }
                else if (dt > _settings.FrameGapResetSec)
                {
                    resetReason = string.Format(CultureInfo.InvariantCulture,
                        "프레임 유실 {0:F2}s (> FrameGapResetSec {1:F2}s)", dt, _settings.FrameGapResetSec);
                    dt = 0.0;
                }
            }

            // Face.id 변경 = 다른 사람. 프레임 유실 리셋이 이미 잡혔으면 그 사유를 우선한다.
            // id 가 -1(미검출)인 프레임은 비교 대상에서 제외한다.
            if (resetReason == null && _settings.ResetOnTrackIdChange &&
                obs.FaceTrackId >= 0 && _lastValidTrackId >= 0 && obs.FaceTrackId != _lastValidTrackId)
            {
                resetReason = string.Format(CultureInfo.InvariantCulture,
                    "Face.id 변경 {0} -> {1}", _lastValidTrackId, obs.FaceTrackId);
            }

            if (resetReason != null)
            {
                ResetInternal(resetReason);
                // 관측하지 않은(또는 다른 사람의) 시간은 누적 통계에 더하지 않는다.
                dt = 0.0;
            }

            if (obs.FaceTrackId >= 0) _lastValidTrackId = obs.FaceTrackId;

            // 리셋 직후 / 첫 프레임이면 상태 진입 시각을 현재로 맞춘다.
            if (!_hasPrev) _stateEnteredSec = now;

            // ---------- 2) 누적 통계 (직전 상태 기준, left-endpoint) ----------
            if (dt > 0.0)
            {
                stats.Accumulate(_state, _prevPresence, dt);
                _observedSec += dt;
            }

            PushFpsSample(dt);
            PrunePerclos(now);

            // ---------- 3) 신호 갱신 ----------
            bool absent = (eye.Presence == Presence.Absent);

            if (absent)
            {
                if (!_faceLost)
                {
                    // 얼굴 유실 진입 프레임
                    _faceLost = true;
                    _noFaceSec = 0.0;
                    _blinkCandidate = false;

                    // [엎드림 휴리스틱] 사라지기 직전에 이미 눈을 감고 있었다면
                    // 이석이 아니라 책상에 엎드린 것으로 본다. ClosedEyeSec 은 얼린다.
                    if (_settings.EnableSlumpHeuristic && _closedEyeSec >= _settings.ClosedEyeSuspectSec)
                    {
                        _slumpSuspected = true;
                    }
                    else
                    {
                        _slumpSuspected = false;
                        _closedEyeSec = 0.0;   // 그냥 나간 것 — 눈 감김 누적은 버린다.
                    }
                }

                _noFaceSec += dt;
                _unknownSec = 0.0;   // 부재는 Unknown 유예와 별개 경로다(아래 주석 참조).
            }
            else
            {
                if (_faceLost)
                {
                    // 얼굴 복귀 → 정상 흐름 복귀
                    _faceLost = false;
                    _noFaceSec = 0.0;
                    _slumpSuspected = false;
                }

                if (eye.State == EyeState.Unknown)
                {
                    // 품질 게이트 탈락. ClosedEyeSec / NoFaceSec 을 얼린다(누적 금지).
                    _unknownSec += dt;
                    _blinkCandidate = false;   // Unknown 이 끼면 blink 시퀀스는 무효
                }
                else
                {
                    _unknownSec = 0.0;

                    if (eye.State == EyeState.Closed)
                    {
                        if (_prevEyeState == EyeState.Open)
                        {
                            // Open → Closed : 새 감김 구간 시작
                            _blinkCandidate = true;
                            _closedEyeSec = 0.0;
                        }
                        _closedEyeSec += dt;
                        AddPerclosSample(now, dt, true);
                    }
                    else // EyeState.Open
                    {
                        if (_prevEyeState == EyeState.Closed && _blinkCandidate)
                        {
                            // Open → Closed → Open 완성. 감김 지속이 blink 범위면 깜빡임으로 센다.
                            // 범위보다 길면 blink 가 아니라 closure(졸음 신호)이므로 세지 않는다.
                            double closedMs = _closedEyeSec * 1000.0;
                            if (closedMs >= _settings.BlinkMinMs && closedMs <= _settings.BlinkMaxMs)
                                stats.BlinkCount++;
                        }
                        _blinkCandidate = false;
                        _closedEyeSec = 0.0;
                        AddPerclosSample(now, dt, false);
                    }
                }
            }

            _prevEyeState = eye.State;
            double perclos = ComputePerclos();

            // ---------- 4) 상태 전이 결정 ----------
            SeatState target = _state;
            double slumpLimit = SlumpAwayFallbackSec;

            if (absent)
            {
                if (_slumpSuspected && _noFaceSec <= slumpLimit)
                {
                    // 엎드림으로 간주 — Away 로 내려보내지 않고 Drowsy 를 유지/승격한다.
                    target = SeatState.Drowsy;
                }
                else
                {
                    if (_slumpSuspected)
                    {
                        // 상한 초과 — 엎드림 가정을 포기하고 이석으로 전환한다.
                        _slumpSuspected = false;
                        _closedEyeSec = 0.0;
                    }
                    if (_noFaceSec >= _settings.NoFaceConfirmSec) target = SeatState.Away;
                    else if (_noFaceSec >= _settings.NoFaceSuspectSec) target = SeatState.AwaySuspect;
                    // 그 외에는 직전 상태 유지 (짧은 검출 실패로 상태를 흔들지 않는다)
                }
            }
            else if (eye.State == EyeState.Unknown)
            {
                // [Unknown 유예] 짧은 Unknown 은 직전 상태를 그대로 유지한다.
                // 유예를 넘기면 "판정 불가"를 정직하게 노출한다.
                if (_unknownSec > _settings.UnknownGraceSec) target = SeatState.Unknown;
            }
            else
            {
                if (_closedEyeSec >= _settings.ClosedEyeConfirmSec) target = SeatState.Drowsy;
                else if (_closedEyeSec >= _settings.ClosedEyeSuspectSec) target = SeatState.DrowsySuspect;
                else target = SeatState.Awake;
            }

            // ---------- 5) 전이 + 알림 ----------
            if (target != _state)
            {
                _state = target;
                _stateEnteredSec = now;
                EmitTransitionAlerts(target, obs, eye, stats, now, perclos, alerts);
            }

            // ---------- 6) PERCLOS 보조 졸음 의심 신호 ----------
            // window 가 다 차기 전에는 표본이 적어 비율이 튄다. 관측 시간이 window 길이를
            // 채운 뒤에만 알림을 낸다(새 임계값을 만들지 않기 위해 window 길이 자체를 기준으로 쓴다).
            if (_observedSec >= _settings.PerclosWindowSec && perclos > _settings.PerclosSuspectRatio)
            {
                TryEmit(alerts, AlertType.Drowsy, AlertLevel.Warn, MsgPerclos,
                        _settings.PerclosWindowSec, obs, eye, stats, now, perclos);
            }

            // ---------- 7) snapshot ----------
            SeatSnapshot snap = new SeatSnapshot();
            snap.Observation = obs;
            snap.Eye = eye;
            snap.State = _state;
            snap.SlumpSuspected = _slumpSuspected;
            snap.ClosedEyeSec = _closedEyeSec;
            snap.NoFaceSec = _noFaceSec;
            snap.Perclos = perclos;
            snap.StateElapsedSec = now - _stateEnteredSec;
            snap.SeatedSec = stats.SeatedSec;
            snap.StudySec = stats.StudySec;
            snap.DrowsySec = stats.DrowsySec;
            snap.AwaySec = stats.AwaySec;
            snap.UnknownSec = stats.UnknownSec;
            snap.DrowsyCount = stats.DrowsyCount;
            snap.AwayCount = stats.AwayCount;
            snap.BlinkCount = stats.BlinkCount;
            snap.Fps = CurrentFps();
            snapshot = snap;

            // ---------- 8) 다음 프레임용 ----------
            _prevMonotonicSec = now;
            _prevPresence = eye.Presence;
            _hasPrev = true;

            return alerts;
        }

        /// <summary>
        /// 외부에서 강제 리셋(카메라 재시작, 세션 시작 등).
        /// 주의: 여기서는 상태머신 내부 카운터만 초기화한다. SessionStats 는 건드리지 않는다
        /// (누적 통계의 수명은 호출자가 결정한다 — Face.id 가 바뀌었을 때 세션 통계까지
        ///  버릴지 말지는 운영 정책이므로 상태머신이 정하지 않는다).
        /// </summary>
        public void Reset(string reason)
        {
            ResetInternal(reason != null ? reason : "외부 요청");
        }

        private void ResetInternal(string reason)
        {
            _state = SeatState.Idle;
            _stateEnteredSec = 0.0;

            _closedEyeSec = 0.0;
            _noFaceSec = 0.0;
            _unknownSec = 0.0;
            _observedSec = 0.0;
            _faceLost = false;
            _slumpSuspected = false;
            _blinkCandidate = false;

            _perclosWindow.Clear();
            _perclosClosedSec = 0.0;
            _perclosOpenSec = 0.0;

            // 다른 사람/다른 구간이므로 cooldown 도 푼다. 그래야 새 인물의 첫 알림이 막히지 않는다.
            _cooldownAt.Clear();
            _pendingRecovery = false;
            _abnormalSinceSec = 0.0;

            _lastValidTrackId = -1;
            _prevEyeState = EyeState.Unknown;
            _prevPresence = Presence.Absent;
            _hasPrev = false;

            _fpsIndex = 0;
            _fpsCount = 0;
            _fpsSum = 0.0;
            for (int i = 0; i < _fpsRing.Length; i++) _fpsRing[i] = 0.0;

            _lastResetReason = reason;
            if (ResetLogger != null)
            {
                try { ResetLogger(reason); }
                catch (Exception) { /* 로깅 실패로 판정이 죽으면 안 된다 */ }
            }
        }

        // ------------------------------------------------------------------
        // 알림
        // ------------------------------------------------------------------

        private void EmitTransitionAlerts(SeatState to, FrameObservation obs, EyeDecision eye,
                                          SessionStats stats, double now, double perclos,
                                          List<AlertEvent> sink)
        {
            switch (to)
            {
                case SeatState.DrowsySuspect:
                    TryEmit(sink, AlertType.Drowsy, AlertLevel.Warn, MsgDrowsySuspect,
                            _closedEyeSec, obs, eye, stats, now, perclos);
                    break;

                case SeatState.Drowsy:
                    // DrowsyCount 는 "확정 전이"에서만 증가한다. cooldown 으로 알림이 눌려도
                    // 전이 자체는 일어났으므로 카운트한다(알림 수 != 사건 수).
                    stats.DrowsyCount++;
                    MarkAbnormal(now);
                    TryEmit(sink, AlertType.Drowsy, AlertLevel.Alert, MsgDrowsyConfirmed,
                            _slumpSuspected ? _closedEyeSec + _noFaceSec : _closedEyeSec,
                            obs, eye, stats, now, perclos);
                    break;

                case SeatState.AwaySuspect:
                    TryEmit(sink, AlertType.Away, AlertLevel.Warn, MsgAwaySuspect,
                            _noFaceSec, obs, eye, stats, now, perclos);
                    break;

                case SeatState.Away:
                    stats.AwayCount++;
                    MarkAbnormal(now);
                    TryEmit(sink, AlertType.Away, AlertLevel.Alert, MsgAwayConfirmed,
                            _noFaceSec, obs, eye, stats, now, perclos);
                    break;

                case SeatState.Awake:
                    // 확정 상태(Drowsy/Away)를 거친 뒤 정상으로 돌아온 경우에만 복귀 알림 1건.
                    // 중간에 Unknown 을 경유해도 복귀 알림이 빠지지 않도록 플래그로 관리한다.
                    if (_pendingRecovery)
                    {
                        TryEmit(sink, AlertType.Recovered, AlertLevel.Clear, MsgRecovered,
                                now - _abnormalSinceSec, obs, eye, stats, now, perclos);
                        _pendingRecovery = false;
                    }
                    break;

                default:
                    // Idle / Unknown 진입은 알림을 내지 않는다.
                    break;
            }
        }

        private void MarkAbnormal(double now)
        {
            if (!_pendingRecovery)
            {
                _pendingRecovery = true;
                _abnormalSinceSec = now;
            }
        }

        /// <summary>
        /// cooldown 을 적용해 알림을 발행한다.
        /// 키는 (Type, Level) 조합이다 — 확정(Alert)은 의심(Warn)과 별개로 카운트된다.
        /// </summary>
        private bool TryEmit(List<AlertEvent> sink, AlertType type, AlertLevel level, string message,
                             double durationSec, FrameObservation obs, EyeDecision eye,
                             SessionStats stats, double now, double perclos)
        {
            int key = ((int)type << 8) | (int)level;
            double lastAt;
            if (_cooldownAt.TryGetValue(key, out lastAt))
            {
                if (now - lastAt < _settings.AlertCooldownSec) return false;
            }
            _cooldownAt[key] = now;

            AlertEvent e = new AlertEvent();
            e.EventId = Guid.NewGuid().ToString("N");
            e.OccurredAt = ToOffset(obs.WallClock);
            e.SeatId = _settings.SeatId;
            e.Type = type;
            e.Level = level;
            e.Message = message;
            e.DurationSec = durationSec;

            AlertEvidence ev = new AlertEvidence();
            ev.EyelidLeft = obs.EyelidLeft;
            ev.EyelidRight = obs.EyelidRight;
            ev.ClosedEyeSec = _closedEyeSec;
            ev.Perclos = perclos;
            ev.LandmarkConfidence = obs.LandmarkConfidence;
            ev.OcclusionLeft = obs.OcclusionLeftEye;
            ev.OcclusionRight = obs.OcclusionRightEye;
            ev.OcclusionMouth = obs.OcclusionMouth;
            ev.FineOcclusion = obs.FineOcclusion;
            ev.Mask = obs.IsMasked;
            ev.Yaw = obs.Yaw;
            ev.Pitch = obs.Pitch;
            ev.Roll = obs.Roll;
            ev.FaceTrackId = obs.FaceTrackId;
            ev.SlumpSuspected = _slumpSuspected;
            ev.UnknownReason = (eye.Reason == UnknownReason.None) ? null : eye.Reason.ToString();
            e.Evidence = ev;

            AlertSessionSummary ss = new AlertSessionSummary();
            ss.SeatedSec = stats.SeatedSec;
            ss.StudySec = stats.StudySec;
            ss.DrowsySec = stats.DrowsySec;
            ss.AwaySec = stats.AwaySec;
            ss.UnknownSec = stats.UnknownSec;
            ss.DrowsyCount = stats.DrowsyCount;
            ss.AwayCount = stats.AwayCount;
            ss.BlinkCount = stats.BlinkCount;
            e.Session = ss;

            sink.Add(e);
            return true;
        }

        /// <summary>
        /// 로그 표기용 벽시계 → DateTimeOffset. 판정에는 쓰지 않는다(CONTRACT.md 0.4).
        /// WallClock 이 비어 있거나 변환 불가면 현재 시각으로 대체한다(알림이 사라지면 안 되므로).
        /// </summary>
        private static DateTimeOffset ToOffset(DateTime wall)
        {
            if (wall == default(DateTime)) return DateTimeOffset.Now;
            try { return new DateTimeOffset(wall); }
            catch (ArgumentException) { return DateTimeOffset.Now; }
        }

        // ------------------------------------------------------------------
        // PERCLOS
        // ------------------------------------------------------------------

        /// <summary>Open/Closed 로 확정된 프레임만 window 에 넣는다. Unknown/부재는 분모에서 제외된다.</summary>
        private void AddPerclosSample(double now, double dt, bool closed)
        {
            if (dt <= 0.0) return;

            PerclosSample s;
            s.AtSec = now;
            s.Dt = dt;
            s.Closed = closed;
            _perclosWindow.Enqueue(s);

            if (closed) _perclosClosedSec += dt;
            else _perclosOpenSec += dt;
        }

        /// <summary>window 밖 표본을 버린다. 메모리가 무한히 늘지 않도록 매 프레임 호출한다.</summary>
        private void PrunePerclos(double now)
        {
            double cutoff = now - _settings.PerclosWindowSec;
            while (_perclosWindow.Count > 0)
            {
                PerclosSample head = _perclosWindow.Peek();
                if (head.AtSec >= cutoff) break;
                _perclosWindow.Dequeue();
                if (head.Closed) _perclosClosedSec -= head.Dt;
                else _perclosOpenSec -= head.Dt;
            }
            // 부동소수 누적 오차 방어
            if (_perclosClosedSec < 0.0) _perclosClosedSec = 0.0;
            if (_perclosOpenSec < 0.0) _perclosOpenSec = 0.0;
        }

        private double ComputePerclos()
        {
            double denom = _perclosClosedSec + _perclosOpenSec;
            if (denom <= 0.0) return 0.0;
            double r = _perclosClosedSec / denom;
            if (r < 0.0) return 0.0;
            if (r > 1.0) return 1.0;
            return r;
        }

        // ------------------------------------------------------------------
        // FPS (표시용)
        // ------------------------------------------------------------------

        private void PushFpsSample(double dt)
        {
            if (dt <= 0.0) return;
            _fpsSum -= _fpsRing[_fpsIndex];
            _fpsRing[_fpsIndex] = dt;
            _fpsSum += dt;
            _fpsIndex = (_fpsIndex + 1) % _fpsRing.Length;
            if (_fpsCount < _fpsRing.Length) _fpsCount++;
        }

        private double CurrentFps()
        {
            if (_fpsCount <= 0 || _fpsSum <= 0.0) return 0.0;
            return _fpsCount / _fpsSum;
        }
    }
}
