using System;
using System.Collections.Generic;
using System.Drawing;
using Etus.DetectSample.Alerts;
using Etus.DetectSample.Analysis;
using Etus.DetectSample.Config;
using Etus.DetectSample.Logging;
using Etus.DetectSample.Sdk;
using Etus.DetectSample.Worker;

namespace Etus.DetectSample
{
    /// <summary>
    /// 각 계층을 배선하는 유일한 지점.
    ///
    /// MainForm 은 순수 뷰이고(FaceSDK / OpenCvSharp / Worker 를 모르는), AnalysisWorker 는 판정을
    /// 모르며, Analysis 계층은 UI 를 모른다. 그 셋을 여기서만 연결한다.
    ///
    /// 스레드 계약(CONTRACT.md 3장):
    ///   - <see cref="OnFrame"/> 과 <see cref="OnWorkerError"/> 는 <b>워커 스레드</b>에서 불린다.
    ///   - 판정(EyeStateGate / SeatStateMachine)도 그 워커 스레드에서 돈다. UI 스레드를 막지 않기 위해서다.
    ///   - UI 로는 MainForm 의 Push*/Set* 만 호출한다. 그 메서드들이 내부에서 BeginInvoke 로 마샬링한다.
    ///   - 따라서 판정 객체들(_gate/_machine/_stats)은 <b>워커 스레드 한 곳에서만</b> 만져진다.
    ///     UI 스레드에서 접근하는 곳을 새로 만들지 말 것. 만들면 lock 이 필요해진다.
    /// </summary>
    public sealed class AppController : IDisposable
    {
        readonly MainForm _form;
        readonly AppSettings _settings;

        readonly FaceEngine _engine;
        readonly AnalysisWorker _worker;

        // --- 워커 스레드 전용 ---
        readonly EyeStateGate _gate;
        readonly SeatStateMachine _machine;
        readonly SessionStats _stats;

        readonly AlertDispatcher _dispatcher;
        readonly FrameCsvLogger _frameLog;
        readonly AlertJsonlLogger _alertLog;
        readonly AlertSnapshotWriter _snapshotWriter;

        SeatSnapshot _lastSnapshot;
        bool _disposed;

        /// <summary>증거 스냅샷이 저장되는 폴더. 스냅샷 뷰어를 열 때 쓴다.</summary>
        public string SnapshotDirectory { get { return _snapshotWriter.Directory; } }

        public AppController(MainForm form)
        {
            if (form == null) throw new ArgumentNullException("form");
            _form = form;

            List<string> warnings;
            _settings = AppSettingsLoader.Load(out warnings);

            _gate = new EyeStateGate(_settings);
            _machine = new SeatStateMachine(_settings);
            _stats = new SessionStats(_settings);

            _frameLog = new FrameCsvLogger(_settings, OnComponentError);
            _alertLog = new AlertJsonlLogger(_settings, OnComponentError);
            _snapshotWriter = new AlertSnapshotWriter(_settings, OnComponentError);

            _dispatcher = new AlertDispatcher(OnComponentError);
            _dispatcher.AddSink("jsonl", _alertLog.Write);
            _dispatcher.AddConsoleSink();
            _dispatcher.AddUiSink(PushAlertToUi);

            // 상태머신이 카운터를 리셋할 때마다 CSV 로 남긴다.
            // Face.id 가 실제로 얼마나 자주 튀는지는 Windows 실기에서만 알 수 있고,
            // 너무 잦으면 ResetOnTrackIdChange 를 꺼야 한다(README 참조).
            _machine.ResetLogger = OnComponentError;

            // 기본 생성자가 App.config 의 EnableLandmark106 을 반영한다.
            // 여기서 CaptureLandmarks 를 덮어쓰면 그 설정 키가 무력화되므로 건드리지 않는다.
            _engine = new FaceEngine(_settings);

            _worker = new AnalysisWorker(_settings, _engine, OnFrame, OnWorkerError);
            _worker.EngineInitialized = OnEngineInitialized;
            _worker.CameraOpened = OnCameraOpened;
            _worker.Progress = OnProgress;

            _form.StartRequested += OnStartRequested;
            _form.StopRequested += OnStopRequested;
            _form.CameraSelected += OnCameraSelected;
            _form.SnapshotsRequested += OnSnapshotsRequested;
            _form.FormClosing += OnFormClosing;

            foreach (string w in warnings)
                _form.SetError("설정 경고: " + w);

            _form.SetRunning(false);
            _form.SetStatus(_engine.ModelPath, "초기화 대기", LogPaths.ResolveDirectory(_settings.LogDirectory));
        }

        /// <summary>MainForm.Load 이후에 호출한다. 카메라 목록 열거는 느려서 생성자에서 하지 않는다.</summary>
        public void Prepare()
        {
            try
            {
                string[] cams = Capture.CameraCapture.EnumerateCameras(4);
                _form.SetCameraList(cams);
            }
            catch (Exception ex)
            {
                _form.SetCameraList(new string[0]);
                _form.SetError("카메라 목록을 읽지 못했습니다: " + ex.Message);
            }
        }

        //
        // UI → 워커
        //

        void OnStartRequested(object sender, EventArgs e)
        {
            try
            {
                // 새 세션이므로 누적을 비운다. 워커가 멈춰 있는 지금이 유일하게 안전한 시점이다.
                _stats.Reset();
                _machine.Reset("세션 시작");
                _lastSnapshot = null;

                _worker.Start();
                _form.SetRunning(true);
            }
            catch (Exception ex)
            {
                _form.SetError("시작하지 못했습니다: " + ex.Message);
                _form.SetRunning(false);
            }
        }

        void OnStopRequested(object sender, EventArgs e)
        {
            StopAndFlush();
        }

        void OnCameraSelected(object sender, int index)
        {
            _worker.SetCameraIndex(index);
        }

        /// <summary>
        /// 증거 스냅샷 폴더를 탐색기로 연다. jpg 파일명에 시각/유형/레벨이 이미 인코딩돼 있으므로
        /// 탐색기 미리보기/정렬만으로 별도 뷰어 없이 확인할 수 있다("Simple is Best").
        /// </summary>
        void OnSnapshotsRequested(object sender, EventArgs e)
        {
            try
            {
                LogPaths.EnsureDirectory(SnapshotDirectory);
                System.Diagnostics.Process.Start("explorer.exe", "\"" + SnapshotDirectory + "\"");
            }
            catch (Exception ex)
            {
                _form.SetError("스냅샷 폴더를 열지 못했습니다: " + ex.Message);
            }
        }

        //
        // 워커 → 판정 → UI  (전부 워커 스레드)
        //

        void OnFrame(FrameObservation obs, Bitmap preview, double fps)
        {
            EyeDecision eye = _gate.Evaluate(obs);

            SeatSnapshot snap;
            IList<AlertEvent> alerts = _machine.Push(obs, eye, _stats, out snap);

            snap.Fps = fps;
            _lastSnapshot = snap;

            _frameLog.Log(snap);

            // preview 의 소유자는 워커다. MainForm 은 이 호출 안에서 자기 버퍼로 복사만 하고
            // 참조를 들고 있지 않는다(MainForm 상단 소유권 주석 참조). 그래서 여기서 Dispose 하지 않는다.
            _form.PushSnapshot(snap, preview);

            if (alerts != null && alerts.Count > 0)
            {
                _dispatcher.Dispatch(alerts);

                // 확정(Alert) 알림의 증거 스냅샷을 남긴다. preview 는 이 콜백 안에서만 유효하므로
                // (워커가 프레임마다 재사용하는 버퍼) 반드시 여기서 동기적으로 저장한다.
                for (int i = 0; i < alerts.Count; i++)
                    _snapshotWriter.Write(alerts[i], preview);
            }
        }

        void PushAlertToUi(AlertEvent e)
        {
            _form.PushAlert(e, AlertJson.ToPrettyJson(e));
        }

        void OnEngineInitialized(InitResult r)
        {
            string logDir = LogPaths.ResolveDirectory(_settings.LogDirectory);

            if (r.Ok)
            {
                string info = "FaceSDK " + FaceSDKVersion() + " · 초기화 OK (" + r.ElapsedMs + "ms)";

                if (!_engine.FineOcclusionAvailable)
                    info += " · FineOcclusion 사용 불가";

                _form.SetStatus(r.ModelPath, "초기화 OK (" + r.ElapsedMs + "ms)", logDir);
                _form.SetEngineInfo(info);
            }
            else
            {
                _form.SetStatus(r.ModelPath, "초기화 실패", logDir);
                _form.SetError(r.ErrorTitle + Environment.NewLine + r.ErrorDetail);
                _form.SetRunning(false);
            }
        }

        void OnCameraOpened(string summary)
        {
            _form.SetEngineInfo(summary);
        }

        void OnProgress(string message)
        {
            _form.SetProgress(message);
        }

        void OnWorkerError(string message)
        {
            _form.SetError(message);
        }

        void OnComponentError(string message)
        {
            // 로거/디스패처/상태머신의 비치명적 사건. 앱을 멈추지 않는다.
            _form.SetError(message);
        }

        //
        // 종료
        //

        void OnFormClosing(object sender, System.Windows.Forms.FormClosingEventArgs e)
        {
            StopAndFlush();
        }

        /// <summary>
        /// 순서가 중요하다. 워커를 먼저 join 해야 판정 객체와 로거에 더 이상 쓰기가 들어오지 않는다.
        /// 그 다음에야 세션 요약을 쓰고 파일을 닫을 수 있다.
        /// </summary>
        void StopAndFlush()
        {
            try
            {
                _worker.Stop();          // _exit=true → Join → 카메라/프리뷰 해제
            }
            catch (Exception ex)
            {
                _form.SetError("정지 중 오류: " + ex.Message);
            }

            try
            {
                _frameLog.Flush();

                // 한 프레임도 못 받았으면 요약을 쓸 것이 없다.
                if (_lastSnapshot != null)
                {
                    string path = _frameLog.WriteSessionSummary(_stats, _lastSnapshot);

                    if (!string.IsNullOrEmpty(path))
                        _form.SetStatus(_engine.ModelPath, "세션 요약 저장됨", path);
                }
            }
            catch (Exception ex)
            {
                _form.SetError("로그 마무리 중 오류: " + ex.Message);
            }

            _form.SetRunning(false);
        }

        static string FaceSDKVersion()
        {
            try { return Alchera.FaceSDK.FaceSDK.VER_STR; }
            catch (Exception) { return "?"; }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            StopAndFlush();

            // 워커가 join 된 뒤에야 SDK 를 내린다. 순서를 바꾸면 분석 중인 스레드가
            // 해제된 네이티브를 호출한다(CONTRACT.md 3장).
            try { _worker.Dispose(); } catch (Exception) { }
            try { _engine.Dispose(); } catch (Exception) { }
            try { _frameLog.Dispose(); } catch (Exception) { }
        }
    }
}
