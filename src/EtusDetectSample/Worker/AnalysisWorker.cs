using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using Etus.DetectSample.Analysis;
using Etus.DetectSample.Capture;
using Etus.DetectSample.Config;
using Etus.DetectSample.Sdk;

namespace Etus.DetectSample.Worker
{
    /// <summary>
    /// 카메라 캡처 + FaceSDK 분석 전담 스레드 1개.
    ///
    /// CONTRACT.md 3장:
    ///  - VideoCapture 와 FaceSDK 는 이 스레드가 독점 소유한다.
    ///  - UI → 워커 명령은 lock + Queue.
    ///  - 워커 → UI 는 BeginInvoke 만. (이 클래스의 모든 콜백은 <b>워커 스레드에서</b> 불린다.
    ///    UI 를 건드리려면 호출 측에서 반드시 BeginInvoke 로 넘길 것. Invoke 는 종료 시 데드락.)
    ///  - 종료 플래그는 volatile bool.
    ///
    /// 판정(EyeStateGate / SeatStateMachine)은 <b>여기서 하지 않는다.</b>
    /// onFrame 콜백 안에서 호출 측이 돌린다. 그래야 Analysis 계층과 커플링되지 않는다.
    /// </summary>
    public sealed class AnalysisWorker : IDisposable
    {
        const int FpsWindowFrames = 30;
        const int JoinTimeoutMs = 3000;

        readonly AppSettings _settings;
        readonly FaceEngine _engine;
        readonly Action<FrameObservation, Bitmap, double> _onFrame;
        readonly Action<string> _onError;

        readonly object _cmdLock = new object();
        readonly Queue<Action> _commands = new Queue<Action>();

        Thread _thread;
        volatile bool _exit;
        volatile bool _running;
        bool _disposed;

        // --- 워커 스레드 전용 (다른 스레드에서 만지지 말 것) ---
        CameraCapture _capture;
        BgrBitmap _preview;
        Stopwatch _clock;

        readonly double[] _fpsRing = new double[FpsWindowFrames];
        int _fpsIdx;
        int _fpsCount;

        // 런타임에 바뀔 수 있는 설정. 변경은 반드시 명령 큐를 통해서만.
        int _cameraIndex;
        bool _flipHorizontal;
        int _rotateDegrees;
        double _targetFps;

        /// <summary>워커가 SDK 를 초기화한 뒤 호출된다. <b>워커 스레드에서 불린다.</b></summary>
        public Action<InitResult> EngineInitialized { get; set; }

        /// <summary>카메라를 열거나 다시 연 직후 호출된다(요약 문자열). <b>워커 스레드에서 불린다.</b></summary>
        public Action<string> CameraOpened { get; set; }

        public bool IsRunning { get { return _running; } }

        /// <summary>마지막으로 onError 로 넘긴 메시지.</summary>
        public string LastError { get; private set; }

        /// <summary>카메라가 실제로 열어 준 캡처 크기.</summary>
        public int CaptureFrameWidth { get; private set; }
        public int CaptureFrameHeight { get; private set; }

        /// <summary>SDK 에 실제로 넘어간 이미지 크기. FrameObservation 의 좌표계 기준이다.</summary>
        public int AnalyzeFrameWidth { get; private set; }
        public int AnalyzeFrameHeight { get; private set; }

        /// <summary>최근 30프레임 이동평균 FPS.</summary>
        public double Fps { get; private set; }

        /// <summary>지금까지 처리한 프레임 수.</summary>
        public long ProcessedFrames { get; private set; }

        /// <param name="s">설정. 워커는 이 객체를 수정하지 않는다.</param>
        /// <param name="engine">
        /// 초기화 전이어도 된다. 아직 초기화되지 않았으면 워커가 <b>자기 스레드에서</b> Initialize 한다
        /// (CONTRACT.md 3장 — UI 스레드에서 SDK 를 호출하지 않는다). 결과는 <see cref="EngineInitialized"/> 로 알린다.
        /// <b>워커는 engine 을 Dispose 하지 않는다.</b> 만든 쪽이 책임진다.
        /// </param>
        /// <param name="onFrame">obs, preview, fps. 워커 스레드에서 불린다. 예외가 나도 워커는 죽지 않는다.</param>
        /// <param name="onError">사용자에게 보여줄 오류 문구. 워커 스레드에서 불린다.</param>
        public AnalysisWorker(AppSettings s, FaceEngine engine,
                              Action<FrameObservation, Bitmap, double> onFrame,
                              Action<string> onError)
        {
            if (s == null) throw new ArgumentNullException("s");
            if (engine == null) throw new ArgumentNullException("engine");

            _settings = s;
            _engine = engine;
            _onFrame = onFrame;
            _onError = onError;

            _cameraIndex = s.CameraIndex;
            _flipHorizontal = s.FlipHorizontal;
            _rotateDegrees = s.SrcRotateDegrees;
            _targetFps = s.TargetAnalyzeFps;
        }

        //
        // 수명 주기
        //

        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException("AnalysisWorker");

            if (_thread != null)
            {
                // 이미 돌고 있으면 아무것도 하지 않는다.
                if (_thread.IsAlive) return;

                // 스레드가 스스로 끝난 경우(카메라 열기 실패, SDK 초기화 실패 등).
                // Stop() 을 거치지 않아 _thread 가 남아 있는데, 이걸 그냥 return 하면
                // 사용자가 원인을 고치고 '시작' 을 다시 눌러도 아무 반응이 없다.
                _thread = null;
            }

            _exit = false;
            _running = true;

            _thread = new Thread(Run);
            _thread.IsBackground = true;   // UI 가 먼저 죽어도 프로세스가 남지 않게
            _thread.Name = "EtusAnalysisWorker";
            _thread.Start();
        }

        /// <summary>
        /// 워커를 세운다. 자원(카메라/미리보기)은 워커 스레드의 finally 에서 스스로 정리한다
        /// (cross-thread dispose 를 피하기 위함).
        /// Join 이 타임아웃되면 자원을 건드리지 않고 오류만 보고한다 — 억지로 내리면 native 가 죽는다.
        /// </summary>
        public void Stop()
        {
            _exit = true;

            Thread t = _thread;
            if (t == null) return;

            if (!t.Join(JoinTimeoutMs))
            {
                SafeError("워커 스레드가 " + (JoinTimeoutMs / 1000) + "초 안에 종료되지 않았습니다. " +
                          "카메라/SDK 자원이 정리되지 않았을 수 있습니다.");
            }

            _thread = null;
            _running = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Stop();
            // _engine 은 여기서 Dispose 하지 않는다. 만든 쪽(MainForm)이 워커 종료 후에 내린다.
        }

        //
        // UI → 워커 명령 (lock + Queue)
        //

        /// <summary>워커 스레드에서 실행할 작업을 넣는다. 어느 스레드에서든 호출 가능.</summary>
        public void Post(Action command)
        {
            if (command == null) return;

            lock (_cmdLock)
            {
                _commands.Enqueue(command);
            }
        }

        public void SetFlipHorizontal(bool flip)
        {
            Post(() =>
            {
                _flipHorizontal = flip;
                if (_capture != null) _capture.FlipHorizontal = flip;
            });
        }

        public void SetRotation(int degrees)
        {
            Post(() =>
            {
                _rotateDegrees = degrees;
                if (_capture != null) _capture.RotateDegrees = degrees;
            });
        }

        public void SetTargetFps(double fps)
        {
            Post(() => { _targetFps = fps < 0 ? 0 : fps; });
        }

        public void SetCaptureLandmarks(bool on)
        {
            Post(() => { _engine.CaptureLandmarks = on; });
        }

        /// <summary>카메라를 닫고 다른 인덱스로 다시 연다.</summary>
        public void SetCameraIndex(int index)
        {
            Post(() =>
            {
                _cameraIndex = index;
                ReopenCamera();
            });
        }

        //
        // 워커 루프
        //

        void Run()
        {
            try
            {
                _clock = Stopwatch.StartNew();

                // --- 1) SDK 초기화 (UI 스레드가 아니라 여기서) ---
                if (!_engine.IsInitialized)
                {
                    InitResult ir = _engine.Initialize();

                    Action<InitResult> cb = EngineInitialized;
                    if (cb != null)
                    {
                        try { cb(ir); }
                        catch (Exception ex) { SafeError("EngineInitialized 콜백 예외: " + ex.Message); }
                    }

                    if (!ir.Ok)
                    {
                        SafeError(ir.ErrorTitle + Environment.NewLine + ir.ErrorDetail);
                        return;
                    }
                }

                // --- 2) 카메라 ---
                // 워커가 멈춰 있는 동안 쌓인 명령을 먼저 반영한다.
                // 예: 정지 상태에서 SetCameraIndex(1) 을 눌러 두고 시작하면,
                //     여기서 비우지 않을 경우 인덱스 0 으로 한 번 열었다가 다시 여는 낭비가 생긴다.
                //     (ReopenCamera 는 _capture==null 이면 조기 반환하므로 여기서는 값만 갱신된다)
                DrainCommands();
                if (_exit) return;

                _capture = new CameraCapture();
                ApplySettingsToCapture();

                string camErr;
                if (!_capture.Open(_cameraIndex, _settings.CaptureWidth, _settings.CaptureHeight,
                                   _settings.CaptureCodec, out camErr))
                {
                    SafeError(camErr);
                    return;
                }

                RaiseCameraOpened();

                _preview = new BgrBitmap();

                long frameIndex = 0;
                int readFailStreak = 0;

                // --- 3) 루프 ---
                while (!_exit)
                {
                    long loopStart = Stopwatch.GetTimestamp();

                    DrainCommands();
                    if (_exit) break;

                    if (_capture == null || !_capture.IsOpen)
                    {
                        SafeError("카메라가 열려 있지 않습니다. 연결을 확인하고 다시 시작하세요.");
                        break;
                    }

                    if (!_capture.Read())
                    {
                        readFailStreak++;
                        // 매 프레임 알림을 쏟아내면 UI 가 마비된다. 처음과 50회마다만 보고.
                        if (readFailStreak == 1 || readFailStreak % 50 == 0)
                        {
                            SafeError("카메라 프레임을 받지 못했습니다 (연속 " + readFailStreak + "회). " +
                                      "카메라가 뽑혔거나 다른 프로그램이 점유했을 수 있습니다.");
                        }
                        Thread.Sleep(30);
                        continue;
                    }

                    readFailStreak = 0;

                    byte[] bgr = _capture.GetBgr();
                    if (bgr == null)
                    {
                        SafeError("프레임 형식이 예상과 다릅니다 (BGR 8UC3 연속 버퍼가 아님). 이 프레임은 건너뜁니다.");
                        Thread.Sleep(30);
                        continue;
                    }

                    int w = _capture.AnalyzeFrameWidth;
                    int h = _capture.AnalyzeFrameHeight;

                    AnalyzeFrameWidth = w;
                    AnalyzeFrameHeight = h;

                    // 판정 시간축은 Stopwatch 만 쓴다 (CONTRACT.md 0-4).
                    double monotonicSec = _clock.Elapsed.TotalSeconds;

                    FrameObservation obs = _engine.Analyze(bgr, w, h, frameIndex, monotonicSec);

                    // 프리뷰는 캡처 원본이 아니라 'SDK 에 넘긴 바로 그 분석 이미지'(기본 720x1280)다.
                    // CONTRACT.md 부록 A-7: FrameObservation 의 box/landmark 는 분석 이미지 좌표이므로,
                    // 캡처 원본을 프리뷰로 쓰면 회전/크롭/스케일만큼 오버레이가 어긋난다.
                    // bgr 은 CameraCapture 가 전처리를 끝낸 버퍼이고 w/h 도 그 크기다 → 그대로 쓰면 된다.
                    Bitmap preview = _preview.Update(bgr, w, h);

                    double fps = PushFps(monotonicSec);

                    Fps = fps;
                    ProcessedFrames = frameIndex + 1;

                    // 콜백에서 예외가 나도 워커는 계속 돈다.
                    if (_onFrame != null)
                    {
                        try
                        {
                            _onFrame(obs, preview, fps);
                        }
                        catch (Exception ex)
                        {
                            SafeError("onFrame 콜백 예외: " + ex.GetType().Name + ": " + ex.Message);
                        }
                    }

                    frameIndex++;

                    Throttle(loopStart);
                }
            }
            catch (Exception ex)
            {
                SafeError("워커 스레드 예외: " + ex.GetType().Name + ": " + ex.Message +
                          Environment.NewLine + ex.StackTrace);
            }
            finally
            {
                // 워커가 만든 자원은 워커 스레드가 정리한다.
                //
                // 단 _preview 는 Dispose 하지 않는다.
                // 마지막 프레임의 Bitmap 이 BeginInvoke 로 UI 에 넘어가 PictureBox.Image 에 걸려 있을 수 있고,
                // 그 상태에서 Dispose 하면 다음 Paint 에서 "매개 변수가 잘못되었습니다" 로 죽는다.
                // 종료 경로이므로 GDI finalizer 에 맡긴다.
                _preview = null;

                if (_capture != null)
                {
                    try { _capture.Dispose(); } catch (Exception) { }
                    _capture = null;
                }

                _running = false;
            }
        }

        void DrainCommands()
        {
            while (true)
            {
                Action cmd = null;

                lock (_cmdLock)
                {
                    if (_commands.Count > 0) cmd = _commands.Dequeue();
                }

                if (cmd == null) break;

                try { cmd(); }
                catch (Exception ex) { SafeError("명령 처리 예외: " + ex.GetType().Name + ": " + ex.Message); }
            }
        }

        void ApplySettingsToCapture()
        {
            if (_capture == null) return;

            // CaptureApi 는 AppSettings 에 없는 확장 키다. 워커가 직접 읽는다.
            _capture.ApiPreference = AppSettingsLoader.GetString("CaptureApi", "ANY");

            _capture.FlipHorizontal = _flipHorizontal;
            _capture.RotateDegrees = _rotateDegrees;
            _capture.EnableAspectCrop = _settings.EnableAspectCrop;
            _capture.EnableAnalyzeResize = _settings.EnableAnalyzeResize;
            _capture.AnalyzeWidth = _settings.AnalyzeWidth;
            _capture.AnalyzeHeight = _settings.AnalyzeHeight;
        }

        void ReopenCamera()
        {
            if (_capture == null) return;

            _capture.Close();
            ApplySettingsToCapture();

            string err;
            if (!_capture.Open(_cameraIndex, _settings.CaptureWidth, _settings.CaptureHeight,
                               _settings.CaptureCodec, out err))
            {
                SafeError(err);
                return;
            }

            ResetFps();
            RaiseCameraOpened();
        }

        void RaiseCameraOpened()
        {
            if (_capture == null) return;

            CaptureFrameWidth = _capture.FrameWidth;
            CaptureFrameHeight = _capture.FrameHeight;

            string summary =
                "카메라 " + _cameraIndex + " 열림: 캡처 " +
                _capture.FrameWidth + "x" + _capture.FrameHeight +
                " (" + _capture.OpenedApiName + ")" +
                " → 분석 " + _settings.AnalyzeWidth + "x" + _settings.AnalyzeHeight +
                (_rotateDegrees != 0 ? ", 회전 " + _rotateDegrees + "도" : "") +
                (_flipHorizontal ? ", 좌우반전" : "");

            Action<string> cb = CameraOpened;
            if (cb == null) return;

            try { cb(summary); }
            catch (Exception ex) { SafeError("CameraOpened 콜백 예외: " + ex.Message); }
        }

        //
        // FPS (최근 30프레임 이동평균)
        //

        void ResetFps()
        {
            _fpsIdx = 0;
            _fpsCount = 0;
            Fps = 0;
        }

        double PushFps(double nowSec)
        {
            _fpsRing[_fpsIdx] = nowSec;
            _fpsIdx = (_fpsIdx + 1) % _fpsRing.Length;

            if (_fpsCount < _fpsRing.Length) _fpsCount++;
            if (_fpsCount < 2) return 0.0;

            int oldest = (_fpsIdx - _fpsCount + _fpsRing.Length) % _fpsRing.Length;
            double span = nowSec - _fpsRing[oldest];

            if (span <= 0.0) return 0.0;

            return (_fpsCount - 1) / span;
        }

        //
        // 목표 FPS 유지
        //

        void Throttle(long loopStartTicks)
        {
            double target = _targetFps;
            if (target <= 0.0) return;   // 0 이면 제한 없음

            double targetMs = 1000.0 / target;
            double elapsedMs = (Stopwatch.GetTimestamp() - loopStartTicks) * 1000.0 / Stopwatch.Frequency;

            int sleepMs = (int)(targetMs - elapsedMs);
            if (sleepMs > 0) Thread.Sleep(sleepMs);
        }

        void SafeError(string message)
        {
            LastError = message;

            Action<string> cb = _onError;
            if (cb == null) return;

            try { cb(message); }
            catch (Exception) { /* 오류 보고 경로에서 또 죽으면 답이 없다 */ }
        }
    }
}
