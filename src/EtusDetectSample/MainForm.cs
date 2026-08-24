using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Etus.DetectSample.Alerts;
using Etus.DetectSample.Analysis;

namespace Etus.DetectSample
{
    /// <summary>
    /// FASMH-94 PoC 데모 화면. 순수 View 다.
    ///
    /// 이 폼은 FaceSDK / OpenCvSharp / AnalysisWorker 를 참조하지 않는다.
    /// 워커 스레드가 <see cref="PushSnapshot"/> / <see cref="PushAlert"/> 로 밀어 넣고,
    /// 폼은 조작(카메라 선택 / 시작 / 정지)을 이벤트로만 밖에 알린다.
    ///
    /// 조작은 3개뿐이다. 설정 다이얼로그/탭/마법사는 의도적으로 없다("Simple is Best").
    /// </summary>
    public partial class MainForm : Form
    {
        // ------------------------------------------------------------------
        // 상태 색상
        // ------------------------------------------------------------------
        static readonly Color ColIdle = Color.FromArgb(110, 116, 124);
        static readonly Color ColAwake = Color.FromArgb(29, 145, 84);
        static readonly Color ColSuspect = Color.FromArgb(222, 146, 20);
        static readonly Color ColDanger = Color.FromArgb(198, 48, 48);

        static readonly Color ColKeyText = Color.FromArgb(122, 129, 138);
        static readonly Color ColValText = Color.FromArgb(28, 32, 38);

        static readonly Color RowAlert = Color.FromArgb(255, 226, 226);
        static readonly Color RowWarn = Color.FromArgb(255, 246, 205);
        static readonly Color RowClear = Color.FromArgb(226, 245, 228);

        const int MaxAlertRows = 200;
        const int PayloadPreviewChars = 140;

        // ------------------------------------------------------------------
        // 밖으로 알리는 이벤트 (통합 담당자가 구독)
        // ------------------------------------------------------------------
        public event EventHandler StartRequested;
        public event EventHandler StopRequested;
        public event EventHandler<int> CameraSelected;
        public event EventHandler SnapshotsRequested;

        // ------------------------------------------------------------------
        // 프레임 버퍼 소유권
        //
        // PushSnapshot 은 워커 스레드에서 초당 10회 호출되고, 넘어오는 Bitmap 은
        // 워커가 다음 프레임에 재사용/해제할 수 있는 "빌려준" 객체다. 따라서
        //   (1) UI 스레드가 나중에 그 Bitmap 을 그리는 것은 금지 —  이미 덮어써졌을 수 있다.
        //   (2) UI 스레드가 그 Bitmap 을 Dispose 하는 것도 금지 — 소유자가 아니다.
        //
        // 그래서 폼이 자기 소유의 버퍼 2장을 들고 다음과 같이 처리한다.
        //   - 워커 스레드: PushSnapshot 안에서 _frameLock 을 잡고 preview 내용을
        //     _workBuffer 로 "복사"한다. 이 복사는 호출자(워커)가 아직 Bitmap 을
        //     들고 있는 시점이므로 안전하다.
        //   - UI 스레드: BeginInvoke 콜백에서 _frameLock 을 잡고 _workBuffer 와
        //     _displayBuffer 의 참조를 교환(swap)한다.
        //   - Paint: _displayBuffer 만 읽는다. 락을 잡지 않는다.
        //
        // 두 버퍼는 항상 서로 다른 객체이고, 워커는 _displayBuffer 를 절대 만지지 않으며,
        // swap 과 Paint 는 둘 다 UI 스레드에서 일어나므로 서로 끼어들 수 없다.
        // 결과적으로 프레임당 Bitmap 할당이 0 이고(GC 압력 없음) 소유권 충돌도 없다.
        // ------------------------------------------------------------------
        readonly object _frameLock = new object();
        Bitmap _workBuffer;        // 워커 전용
        Bitmap _displayBuffer;     // UI 전용
        bool _workBufferFilled;
        bool _buffersDisposed;

        SeatSnapshot _pendingSnapshot;   // _frameLock 보호
        SeatSnapshot _displaySnapshot;   // UI 스레드 전용
        int _uiUpdatePending;            // 0/1, Interlocked

        volatile bool _closing;

        readonly MethodInvoker _applySnapshotInvoker;

        // 확정 Alert 효과음 연타 방지
        readonly System.Diagnostics.Stopwatch _soundClock = System.Diagnostics.Stopwatch.StartNew();
        double _lastSoundSec = -3600.0;

        // 배지 재도색을 실제로 바뀐 순간에만 하기 위한 캐시
        bool _badgeInitialized;
        SeatState _lastBadgeState;
        bool _lastSlump;

        bool _suppressCameraEvent;

        // 시작 직후 첫 프레임이 도착할 때까지 lblPreviewOverlay 를 띄워 둘지
        bool _firstFramePending;

        Font _fontValue;
        Font _fontHud;
        Font _fontHudSmall;

        public MainForm()
        {
            InitializeComponent();

            _applySnapshotInvoker = new MethodInvoker(ApplySnapshotOnUi);

            _fontValue = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            _fontHud = new Font(Font.FontFamily, 13F, FontStyle.Bold);
            _fontHudSmall = new Font(Font.FontFamily, 9F, FontStyle.Regular);

            StyleGrid();
            WireEvents();

            SetRunning(false);
            ApplyBadge(SeatState.Idle, false, UnknownReason.None, 0.0);

            toolTip1.SetToolTip(lblDisclaimer,
                "이 PoC 의 한계입니다.\r\n" +
                "· 공부 시간은 '깨어 있는 시간'으로 계산합니다. 휴대폰 사용, 멍하니 있는 상태, 대화는 구분하지 못합니다.\r\n" +
                "· 안면 인식(객체 인식이 아님) 기반이므로 책상에 엎드린 상태와 자리를 비운 상태를 완전히 구분할 수 없습니다.\r\n" +
                "· 임계값은 전부 잠정치이며 현장 재보정이 필요합니다.");
            toolTip1.SetToolTip(lblAlertHeader,
                "행을 더블클릭하면 중앙 인포데스크로 전달 가능한 전체 JSON payload 를 볼 수 있습니다.");
        }

        /// <summary>디자이너 파일 Dispose(bool) 에서 호출된다. 폼이 소유한 GDI 리소스를 회수한다.</summary>
        void DisposeOwnedResources()
        {
            lock (_frameLock)
            {
                _buffersDisposed = true;
                if (_workBuffer != null) { _workBuffer.Dispose(); _workBuffer = null; }
                if (_displayBuffer != null) { _displayBuffer.Dispose(); _displayBuffer = null; }
                _workBufferFilled = false;
            }
            if (_fontValue != null) { _fontValue.Dispose(); _fontValue = null; }
            if (_fontHud != null) { _fontHud.Dispose(); _fontHud = null; }
            if (_fontHudSmall != null) { _fontHudSmall.Dispose(); _fontHudSmall = null; }
        }

        // ==================================================================
        //  초기 스타일 / 이벤트 배선
        // ==================================================================

        void StyleGrid()
        {
            // 값 라벨은 시연 거리에서 읽혀야 하므로 굵게 키운다.
            // (디자이너 InitializeComponent 를 단순하게 유지하려고 여기서 일괄 적용)
            Label[] keys = new Label[]
            {
                lblEyeK, lblClosedK, lblPerclosK, lblNoFaceK, lblLandmarkK, lblMaskK,
                lblOcclK, lblFineK, lblPoseK, lblFaceIdK,
                lblSeatedK, lblStudyK, lblDrowsyK, lblAwayK, lblUnknownK, lblBlinkK
            };
            Label[] values = new Label[]
            {
                lblEyeV, lblClosedV, lblPerclosV, lblNoFaceV, lblLandmarkV, lblMaskV,
                lblOcclV, lblFineV, lblPoseV, lblFaceIdV,
                lblSeatedV, lblStudyV, lblDrowsyV, lblAwayV, lblUnknownV, lblBlinkV
            };

            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].ForeColor = ColKeyText;
            }
            for (int i = 0; i < values.Length; i++)
            {
                values[i].Font = _fontValue;
                values[i].ForeColor = ColValText;
            }
        }

        void WireEvents()
        {
            tsBtnStart.Click += TsBtnStart_Click;
            tsBtnStop.Click += TsBtnStop_Click;
            tsBtnSnapshots.Click += TsBtnSnapshots_Click;
            tsCmbCamera.SelectedIndexChanged += TsCmbCamera_SelectedIndexChanged;

            // PictureBox 는 생성자에서 ControlStyles.OptimizedDoubleBuffer 를 켜므로
            // 별도 처리 없이 더블버퍼링된다. Image 는 비워두고 Paint 에서 직접 그린다.
            picPreview.Paint += PicPreview_Paint;
            picPreview.SizeChanged += PicPreview_SizeChanged;

            lvAlerts.DoubleClick += LvAlerts_DoubleClick;
            lvAlerts.KeyDown += LvAlerts_KeyDown;

            lblError.Click += LblError_Click;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            // 취소되지 않은 종료에 한해 워커 → UI 마샬링을 차단한다.
            if (!e.Cancel) _closing = true;
        }

        // ==================================================================
        //  공개 API — 통합 담당자가 호출한다
        // ==================================================================

        /// <summary>
        /// 워커 스레드에서 프레임 1장 분의 결과를 밀어 넣는다. 초당 10회 호출을 전제로 한다.
        /// preview 의 소유권은 호출자에게 남는다(내부에서 복사한다).
        ///
        /// 주의: preview 는 <paramref name="snapshot"/>.Observation 을 만들어낸 것과
        /// <b>동일한 좌표계의 이미지</b>여야 한다. 분석용 이미지와 다른 크기/좌우반전 이미지를
        /// 넘기면 face box / landmark 오버레이가 어긋난다(크기 차이는 자동 스케일되지만
        /// crop / flip 은 보정할 수 없다).
        /// </summary>
        public void PushSnapshot(SeatSnapshot snapshot, Bitmap preview)
        {
            if (snapshot == null) return;
            if (_closing) return;

            lock (_frameLock)
            {
                if (_buffersDisposed) return;
                _pendingSnapshot = snapshot;

                if (preview != null && preview.Width > 0 && preview.Height > 0)
                {
                    if (_workBuffer == null ||
                        _workBuffer.Width != preview.Width ||
                        _workBuffer.Height != preview.Height)
                    {
                        // _workBuffer 는 워커 전용이므로 여기서 해제해도 UI 와 충돌하지 않는다.
                        if (_workBuffer != null) { _workBuffer.Dispose(); _workBuffer = null; }
                        _workBuffer = new Bitmap(preview.Width, preview.Height, PixelFormat.Format24bppRgb);
                    }

                    using (Graphics g = Graphics.FromImage(_workBuffer))
                    {
                        g.CompositingMode = CompositingMode.SourceCopy;
                        g.DrawImageUnscaled(preview, 0, 0);
                    }
                    _workBufferFilled = true;
                }
            }

            // coalescing: 이미 대기 중인 갱신이 있으면 새로 걸지 않는다.
            // 위에서 최신 스냅샷/프레임을 이미 덮어썼으므로 대기 중인 콜백이 최신을 집어간다.
            if (Interlocked.Exchange(ref _uiUpdatePending, 1) == 0)
            {
                if (!TryBeginInvoke(_applySnapshotInvoker))
                {
                    Interlocked.Exchange(ref _uiUpdatePending, 0);
                }
            }
        }

        /// <summary>알림 1건을 목록에 추가한다. json 은 AlertDispatcher 가 만든 원본 문자열.</summary>
        public void PushAlert(AlertEvent alert, string json)
        {
            if (alert == null) return;
            AlertEvent a = alert;
            string j = json;
            RunOnUi(delegate { ApplyAlert(a, j); });
        }

        /// <summary>하단 상태바 문구.</summary>
        public void SetStatus(string modelPath, string initResult, string logPath)
        {
            string m = modelPath;
            string i = initResult;
            string l = logPath;
            RunOnUi(delegate
            {
                tsslModel.Text = "모델: " + TailPath(m, 64);
                tsslInit.Text = "초기화: " + (string.IsNullOrEmpty(i) ? "-" : i);
                tsslLog.Text = "로그: " + TailPath(l, 64);
            });
        }

        /// <summary>
        /// 시작 구간(엔진 초기화 → 카메라 오픈) 진행 상황을 상태바와 영상 오버레이에 함께 보여준다.
        /// 첫 프레임이 도착하기 전까지는 화면이 비어 있어 정상 동작 중인지 알기 어렵기 때문이다.
        /// </summary>
        public void SetProgress(string message)
        {
            string m = string.IsNullOrEmpty(message) ? "-" : message;
            RunOnUi(delegate
            {
                tsslInit.Text = "초기화: " + m;
                if (lblPreviewOverlay.Visible) lblPreviewOverlay.Text = m;
            });
        }

        /// <summary>치명적이지 않은 오류를 상단 빨간 띠로 보여준다. 클릭하면 사라진다.</summary>
        public void SetError(string message)
        {
            string m = message;
            RunOnUi(delegate
            {
                if (string.IsNullOrEmpty(m))
                {
                    lblError.Visible = false;
                    lblError.Text = "";
                    return;
                }
                lblError.Text = "오류  ·  " + m.Replace("\r", " ").Replace("\n", " ") + "     (클릭하면 닫힙니다)";
                lblError.Visible = true;
            });
        }

        /// <summary>실행 중이면 시작 버튼과 카메라 선택을 잠근다.</summary>
        public void SetRunning(bool running)
        {
            bool r = running;
            RunOnUi(delegate
            {
                tsBtnStart.Enabled = !r;
                tsBtnStop.Enabled = r;
                tsCmbCamera.Enabled = !r;
                if (!r)
                {
                    tsLblFps.Text = "-- FPS";
                }

                // 시작 시점엔 첫 프레임이 올 때까지 오버레이로 "준비 중"을 보여주고,
                // 정지 시점엔 다음 시작을 위해 오버레이를 숨겨 둔다(마지막 화면이 가려지지 않게).
                _firstFramePending = r;
                lblPreviewOverlay.Visible = r;
                if (r) lblPreviewOverlay.Text = "카메라를 준비하는 중입니다. 잠시만 기다려주세요...";
            });
        }

        /// <summary>
        /// 카메라 목록을 채운다. 폼은 OpenCvSharp 를 참조하지 않으므로 열거는 통합 담당자가 한다.
        /// 이 호출은 CameraSelected 이벤트를 발생시키지 않는다.
        /// </summary>
        public void SetCameraList(string[] names)
        {
            string[] n = names;
            RunOnUi(delegate
            {
                _suppressCameraEvent = true;
                try
                {
                    tsCmbCamera.Items.Clear();
                    if (n != null)
                    {
                        for (int i = 0; i < n.Length; i++)
                        {
                            tsCmbCamera.Items.Add(n[i] == null ? "(이름 없음)" : n[i]);
                        }
                    }
                    if (tsCmbCamera.Items.Count > 0)
                    {
                        tsCmbCamera.SelectedIndex = 0;
                    }
                }
                finally
                {
                    _suppressCameraEvent = false;
                }
            });
        }

        /// <summary>
        /// (선택) 상단 우측 엔진 표기를 바꾼다. 호출하지 않으면 "FaceSDK ·" 로만 표시된다.
        /// SDK 버전은 폼이 알 수 없으므로 임의로 지어내지 않는다.
        /// </summary>
        public void SetEngineInfo(string text)
        {
            string t = text;
            RunOnUi(delegate
            {
                tsLblEngine.Text = string.IsNullOrEmpty(t) ? "FaceSDK  ·" : (t + "  ·");
            });
        }

        // ==================================================================
        //  UI 마샬링
        // ==================================================================

        /// <summary>워커 → UI. Invoke 는 종료 시 데드락이므로 절대 쓰지 않는다.</summary>
        bool TryBeginInvoke(Delegate d)
        {
            if (_closing || IsDisposed || !IsHandleCreated) return false;
            try
            {
                BeginInvoke(d);
                return true;
            }
            catch (ObjectDisposedException) { return false; }
            catch (InvalidOperationException) { return false; }   // 핸들 소멸 경합
        }

        /// <summary>저빈도 갱신용. 핸들 생성 전(= Application.Run 이전, UI 스레드)에는 바로 실행한다.</summary>
        void RunOnUi(MethodInvoker action)
        {
            if (action == null) return;
            if (_closing || IsDisposed) return;

            if (!IsHandleCreated)
            {
                // 폼이 아직 표시되지 않은 시점. 이 경로는 통합 담당자가 Application.Run
                // 직전에 초기값을 넣는 경우이며 그때는 UI 스레드다.
                try { action(); }
                catch (ObjectDisposedException) { }
                return;
            }

            if (!InvokeRequired)
            {
                action();
                return;
            }

            try { BeginInvoke(action); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        // ==================================================================
        //  스냅샷 반영 (UI 스레드)
        // ==================================================================

        void ApplySnapshotOnUi()
        {
            Interlocked.Exchange(ref _uiUpdatePending, 0);
            if (_closing || IsDisposed) return;

            SeatSnapshot snap;
            bool swapped = false;

            lock (_frameLock)
            {
                snap = _pendingSnapshot;
                if (_workBufferFilled && !_buffersDisposed)
                {
                    // 참조 교환. 두 버퍼는 항상 서로 다른 객체다.
                    Bitmap tmp = _displayBuffer;
                    _displayBuffer = _workBuffer;
                    _workBuffer = tmp;
                    _workBufferFilled = false;
                    swapped = true;
                }
            }

            if (snap == null) return;
            _displaySnapshot = snap;

            // 첫 영상 프레임이 실제로 도착한 순간 "준비 중" 오버레이를 내린다.
            if (_firstFramePending && swapped)
            {
                _firstFramePending = false;
                lblPreviewOverlay.Visible = false;
            }

            UpdateBadge(snap);
            UpdateSignals(snap);
            UpdateTotals(snap);

            SetText(tsLblFps, F1(snap.Fps) + " FPS");

            if (swapped || _displayBuffer != null)
            {
                picPreview.Invalidate();
            }
        }

        void UpdateBadge(SeatSnapshot snap)
        {
            UnknownReason reason = snap.State == SeatState.Unknown ? snap.Eye.Reason : UnknownReason.None;
            ApplyBadge(snap.State, snap.SlumpSuspected, reason, snap.StateElapsedSec);
        }

        void ApplyBadge(SeatState state, bool slump, UnknownReason reason, double elapsedSec)
        {
            if (!_badgeInitialized || _lastBadgeState != state || _lastSlump != slump)
            {
                string ko;
                Color col;
                MapState(state, out ko, out col);

                lblStateBig.Text = slump ? (ko + "  (엎드림 의심)") : ko;
                lblStateCode.Text = state.ToString().ToUpperInvariant();

                tblBadge.BackColor = col;
                lblStateBig.BackColor = col;
                lblStateCode.BackColor = col;
                lblStateElapsed.BackColor = col;
                lblUnknownReason.BackColor = col;

                _lastBadgeState = state;
                _lastSlump = slump;
                _badgeInitialized = true;
            }

            SetText(lblStateElapsed, FmtHms(elapsedSec));
            SetText(lblUnknownReason, reason == UnknownReason.None ? "" : ("사유: " + ReasonKo(reason)));
        }

        void UpdateSignals(SeatSnapshot snap)
        {
            FrameObservation obs = snap.Observation;

            // --- 눈 ---
            string eyeText;
            if (obs != null && obs.EyelidValid)
            {
                eyeText = F2(obs.EyelidLeft) + " / " + F2(obs.EyelidRight) + "    " + EyeStateText(snap.Eye.State);
            }
            else
            {
                eyeText = "— / —    " + EyeStateText(snap.Eye.State);
            }
            SetText(lblEyeV, eyeText);

            Color eyeCol;
            switch (snap.Eye.State)
            {
                case EyeState.Open: eyeCol = ColAwake; break;
                case EyeState.Closed: eyeCol = ColDanger; break;
                default: eyeCol = ColIdle; break;
            }
            if (lblEyeV.ForeColor != eyeCol) lblEyeV.ForeColor = eyeCol;

            SetText(lblClosedV, F1(snap.ClosedEyeSec) + " s");
            SetText(lblPerclosV, F1(snap.Perclos * 100.0) + " %");
            SetText(lblNoFaceV, F1(snap.NoFaceSec) + " s");

            if (obs == null)
            {
                SetText(lblLandmarkV, "—");
                SetText(lblMaskV, "—");
                SetText(lblOcclV, "—");
                SetText(lblFineV, "—");
                SetText(lblPoseV, "—");
                SetText(lblFaceIdV, "—");
                return;
            }

            SetText(lblLandmarkV, F2(obs.LandmarkConfidence));

            // Mask 는 FaceSDK 에서 반전되어 있어 IsFaceMasked() 결과(= obs.IsMasked)만 신뢰한다.
            // MaskConfidence 는 원시값이라 참고용으로만 병기한다.
            string maskText;
            if (float.IsNaN(obs.MaskConfidence))
            {
                maskText = obs.IsMasked ? "착용" : "미측정";
            }
            else
            {
                maskText = (obs.IsMasked ? "착용" : "미착용") + "  (" + F2(obs.MaskConfidence) + ")";
            }
            SetText(lblMaskV, maskText);

            SetText(lblOcclV,
                "L " + F2(obs.OcclusionLeftEye) +
                "  R " + F2(obs.OcclusionRightEye) +
                "  M " + F2(obs.OcclusionMouth));

            SetText(lblFineV, obs.FineOcclusionAvailable ? F2(obs.FineOcclusion) : "비활성 (모델 없음)");

            if (obs.FaceDetected)
            {
                SetText(lblPoseV, F0(obs.Yaw) + " / " + F0(obs.Pitch) + " / " + F0(obs.Roll));
                SetText(lblFaceIdV, obs.FaceTrackId < 0 ? "인식 대상 없음" : "정상 인식 중");
            }
            else
            {
                SetText(lblPoseV, "—");
                SetText(lblFaceIdV, "인식 대상 없음");
            }
        }

        void UpdateTotals(SeatSnapshot snap)
        {
            SetText(lblSeatedV, FmtHms(snap.SeatedSec));
            SetText(lblStudyV, FmtHms(snap.StudySec));
            SetText(lblDrowsyV, FmtHms(snap.DrowsySec) + "   (" + snap.DrowsyCount.ToString(CultureInfo.InvariantCulture) + "회)");
            SetText(lblAwayV, FmtHms(snap.AwaySec) + "   (" + snap.AwayCount.ToString(CultureInfo.InvariantCulture) + "회)");
            SetText(lblUnknownV, FmtHms(snap.UnknownSec));
            SetText(lblBlinkV, snap.BlinkCount.ToString(CultureInfo.InvariantCulture) + " 회");
        }

        // ==================================================================
        //  프리뷰 렌더링
        // ==================================================================

        void PicPreview_SizeChanged(object sender, EventArgs e)
        {
            picPreview.Invalidate();
        }

        void PicPreview_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle client = picPreview.ClientRectangle;

            // _displayBuffer 는 UI 스레드만 읽고 쓴다(swap 도 UI 스레드).
            // 워커는 _workBuffer 만 만지므로 여기서 락이 필요 없다.
            Bitmap bmp = _displayBuffer;
            if (bmp == null)
            {
                DrawCenteredMessage(g, client, "카메라 대기 중\n상단의 [시작] 을 누르세요");
                return;
            }

            // 해상도를 상수로 박지 않는다. 매 프레임 실제 Bitmap 크기에서 읽는다.
            // 분석 이미지는 현재 설정 기준 720x1280 세로지만 config 로 바뀔 수 있다.
            Rectangle dest = FitRect(client, bmp.Width, bmp.Height);   // 종횡비 유지 + 레터박스

            g.InterpolationMode = InterpolationMode.Bilinear;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            try
            {
                g.DrawImage(bmp, dest);
            }
            catch (Exception)
            {
                // GDI+ 가 드물게 실패해도 시연이 멈추면 안 된다. 이 프레임만 건너뛴다.
                return;
            }

            DrawOverlay(g, new PreviewMap(dest, bmp.Width, bmp.Height), _displaySnapshot);
        }

        void DrawCenteredMessage(Graphics g, Rectangle client, string text)
        {
            using (StringFormat sf = new StringFormat())
            using (SolidBrush br = new SolidBrush(Color.FromArgb(150, 156, 166)))
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(text, _fontHud ?? Font, br, client, sf);
            }
        }

        /// <summary>
        /// 분석 이미지 픽셀 좌표 → 화면 표시 좌표 변환기.
        ///
        /// FrameObservation 의 BoxX/BoxY/BoxW/BoxH 와 Landmark106 은 SDK 에 넘긴
        /// <b>분석 이미지(현재 설정 기준 720x1280 세로)의 픽셀 좌표</b>다(CONTRACT 부록 A).
        /// 화면에는 종횡비를 유지한 레터박스 사각형에 그리므로 scale + offset 변환이 반드시 필요하다.
        ///
        /// 오버레이(face box / landmark / HUD 텍스트)는 예외 없이 전부 이 구조체를 거친다.
        /// 변환이 여기저기 흩어지면 반드시 어긋나기 때문이다.
        /// 해상도는 상수로 박지 않고 생성 시점에 실제 Bitmap 크기로 받는다.
        /// </summary>
        struct PreviewMap
        {
            /// <summary>레터박스를 뺀, 영상이 실제로 그려진 사각형.</summary>
            public readonly Rectangle Dest;
            readonly float _scaleX;
            readonly float _scaleY;

            public PreviewMap(Rectangle dest, int srcW, int srcH)
            {
                Dest = dest;
                _scaleX = srcW > 0 ? dest.Width / (float)srcW : 1f;
                _scaleY = srcH > 0 ? dest.Height / (float)srcH : 1f;
            }

            /// <summary>분석 이미지 x → 화면 x (offset 포함).</summary>
            public float X(float srcX) { return Dest.X + srcX * _scaleX; }

            /// <summary>분석 이미지 y → 화면 y (offset 포함).</summary>
            public float Y(float srcY) { return Dest.Y + srcY * _scaleY; }

            /// <summary>분석 이미지 폭 → 화면 폭 (offset 없음).</summary>
            public float W(float srcW) { return srcW * _scaleX; }

            /// <summary>분석 이미지 높이 → 화면 높이 (offset 없음).</summary>
            public float H(float srcH) { return srcH * _scaleY; }
        }

        void DrawOverlay(Graphics g, PreviewMap map, SeatSnapshot snap)
        {
            if (snap == null) return;

            string ko;
            Color col;
            MapState(snap.State, out ko, out col);

            FrameObservation obs = snap.Observation;

            // --- face box ---
            if (obs != null && obs.FaceDetected && obs.BoxW > 0f && obs.BoxH > 0f)
            {
                using (Pen p = new Pen(col, 2.5f))
                {
                    g.DrawRectangle(p,
                        map.X(obs.BoxX), map.Y(obs.BoxY),
                        map.W(obs.BoxW), map.H(obs.BoxH));
                }
            }

            // landmark(106점) 오버레이는 화면을 어지럽혀 표시하지 않는다.
            // 감지 박스(위)만 남긴다 — SDK 의 landmark 캡처 자체는 그대로 유지된다.

            // --- 영상 좌상단 HUD ---
            // 세로 프리뷰라 폭이 좁다. HUD 도 같은 변환(map)으로 영상 좌상단에 붙이고,
            // 폭은 영상 폭을 넘지 않도록 자른다.
            string head = snap.SlumpSuspected ? (ko + "  (엎드림 의심)") : ko;
            string sub = FmtHms(snap.StateElapsedSec)
                       + "   감김 " + F1(snap.ClosedEyeSec) + "s"
                       + "   미검출 " + F1(snap.NoFaceSec) + "s";

            Font fHead = _fontHud ?? Font;
            Font fSub = _fontHudSmall ?? Font;

            SizeF headSize = g.MeasureString(head, fHead);
            SizeF subSize = g.MeasureString(sub, fSub);

            float bx = map.X(0f) + 10f;
            float by = map.Y(0f) + 10f;
            float maxW = map.Dest.Width - 20f;
            if (maxW < 40f) return;   // 영상이 너무 작으면 HUD 를 생략한다

            float boxW = Math.Min(Math.Max(headSize.Width, subSize.Width) + 24f, maxW);
            float boxH = headSize.Height + subSize.Height + 14f;

            using (SolidBrush shade = new SolidBrush(Color.FromArgb(165, 0, 0, 0)))
            using (SolidBrush accent = new SolidBrush(col))
            using (SolidBrush white = new SolidBrush(Color.White))
            using (SolidBrush grayText = new SolidBrush(Color.FromArgb(225, 228, 232)))
            using (StringFormat sf = new StringFormat())
            {
                sf.FormatFlags = StringFormatFlags.NoWrap;
                sf.Trimming = StringTrimming.EllipsisCharacter;

                g.FillRectangle(shade, bx, by, boxW, boxH);
                g.FillRectangle(accent, bx, by, 5f, boxH);

                RectangleF rHead = new RectangleF(bx + 13f, by + 5f, boxW - 18f, headSize.Height);
                RectangleF rSub = new RectangleF(bx + 13f, by + 7f + headSize.Height, boxW - 18f, subSize.Height);
                g.DrawString(head, fHead, white, rHead, sf);
                g.DrawString(sub, fSub, grayText, rSub, sf);
            }
        }

        static Rectangle FitRect(Rectangle client, int srcW, int srcH)
        {
            if (srcW <= 0 || srcH <= 0 || client.Width <= 0 || client.Height <= 0) return client;
            double scale = Math.Min(client.Width / (double)srcW, client.Height / (double)srcH);
            int w = (int)Math.Round(srcW * scale);
            int h = (int)Math.Round(srcH * scale);
            if (w < 1) w = 1;
            if (h < 1) h = 1;
            return new Rectangle(client.X + (client.Width - w) / 2,
                                 client.Y + (client.Height - h) / 2, w, h);
        }

        // ==================================================================
        //  Alert 목록
        // ==================================================================

        void ApplyAlert(AlertEvent a, string json)
        {
            if (_closing || IsDisposed) return;

            string time = a.OccurredAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            string preview = CompactJson(json, PayloadPreviewChars);
            if (preview.Length == 0) preview = BuildFallbackPreview(a);

            ListViewItem item = new ListViewItem(new string[]
            {
                time,
                TypeKo(a.Type),
                LevelKo(a.Level),
                a.Message == null ? "" : a.Message,
                preview
            });

            switch (a.Level)
            {
                case AlertLevel.Alert: item.BackColor = RowAlert; break;
                case AlertLevel.Warn: item.BackColor = RowWarn; break;
                case AlertLevel.Clear: item.BackColor = RowClear; break;
            }

            // 더블클릭 팝업에서 쓸 전체 payload. json 이 없으면 요약이라도 남긴다.
            item.Tag = string.IsNullOrEmpty(json) ? BuildFallbackPreview(a) : json;

            lvAlerts.BeginUpdate();
            try
            {
                lvAlerts.Items.Insert(0, item);
                while (lvAlerts.Items.Count > MaxAlertRows)
                {
                    lvAlerts.Items.RemoveAt(lvAlerts.Items.Count - 1);
                }
            }
            finally
            {
                lvAlerts.EndUpdate();
            }

            if (a.Level == AlertLevel.Alert)
            {
                PlayAlertSoundOnce();
            }
        }

        void PlayAlertSoundOnce()
        {
            // 확정 Alert 마다 1회. 연속 재생(연타)은 막는다.
            double now = _soundClock.Elapsed.TotalSeconds;
            if (now - _lastSoundSec < 2.0) return;
            _lastSoundSec = now;
            try { System.Media.SystemSounds.Exclamation.Play(); }
            catch (Exception) { /* 사운드 장치가 없어도 시연은 계속되어야 한다 */ }
        }

        void LvAlerts_DoubleClick(object sender, EventArgs e)
        {
            ShowSelectedPayload();
        }

        void LvAlerts_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ShowSelectedPayload();
                e.Handled = true;
            }
        }

        void ShowSelectedPayload()
        {
            if (lvAlerts.SelectedItems.Count == 0) return;
            ListViewItem item = lvAlerts.SelectedItems[0];
            string json = item.Tag as string;
            if (string.IsNullOrEmpty(json)) return;

            string title = item.SubItems[0].Text + "  ·  " + item.SubItems[1].Text + " " + item.SubItems[2].Text
                         + "  ·  중앙 인포데스크 전달 payload";
            ShowJsonDialog(this, title, json);
        }

        static void ShowJsonDialog(IWin32Window owner, string title, string json)
        {
            using (Form dlg = new Form())
            using (Font mono = new Font("Consolas", 9.75F))
            {
                dlg.Text = title;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.ClientSize = new Size(820, 560);
                dlg.MinimizeBox = false;
                dlg.ShowInTaskbar = false;
                dlg.ShowIcon = false;

                TextBox tb = new TextBox();
                tb.Multiline = true;
                tb.ReadOnly = true;
                tb.WordWrap = false;
                tb.ScrollBars = ScrollBars.Both;
                tb.Dock = DockStyle.Fill;
                tb.BackColor = Color.White;
                tb.Font = mono;
                tb.Text = json;

                Panel bottom = new Panel();
                bottom.Dock = DockStyle.Bottom;
                bottom.Height = 46;
                bottom.Padding = new Padding(8);

                Button btnClose = new Button();
                btnClose.Text = "닫기";
                btnClose.Dock = DockStyle.Right;
                btnClose.Width = 90;
                btnClose.DialogResult = DialogResult.OK;

                Button btnCopy = new Button();
                btnCopy.Text = "클립보드로 복사";
                btnCopy.Dock = DockStyle.Right;
                btnCopy.Width = 140;
                string payload = json;
                btnCopy.Click += delegate
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(payload)) Clipboard.SetText(payload);
                    }
                    catch (System.Runtime.InteropServices.ExternalException)
                    {
                        // 다른 프로세스가 클립보드를 잠근 경우. 무시한다.
                    }
                };

                // Dock=Right 는 나중에 추가된 쪽이 먼저 배치되어 더 오른쪽에 놓인다.
                bottom.Controls.Add(btnCopy);
                bottom.Controls.Add(btnClose);

                dlg.Controls.Add(tb);
                dlg.Controls.Add(bottom);
                dlg.CancelButton = btnClose;

                dlg.ShowDialog(owner);
            }
        }

        // ==================================================================
        //  조작 3개
        // ==================================================================

        void TsBtnStart_Click(object sender, EventArgs e)
        {
            EventHandler h = StartRequested;
            if (h != null) h(this, EventArgs.Empty);
        }

        void TsBtnStop_Click(object sender, EventArgs e)
        {
            EventHandler h = StopRequested;
            if (h != null) h(this, EventArgs.Empty);
        }

        void TsBtnSnapshots_Click(object sender, EventArgs e)
        {
            EventHandler h = SnapshotsRequested;
            if (h != null) h(this, EventArgs.Empty);
        }

        void TsCmbCamera_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressCameraEvent) return;
            int idx = tsCmbCamera.SelectedIndex;
            if (idx < 0) return;
            EventHandler<int> h = CameraSelected;
            if (h != null) h(this, idx);
        }

        void LblError_Click(object sender, EventArgs e)
        {
            lblError.Visible = false;
        }

        // ==================================================================
        //  포맷 헬퍼
        // ==================================================================

        static void SetText(Control c, string text)
        {
            // 초당 10회 갱신되므로 값이 바뀐 것만 쓴다(불필요한 Invalidate 방지).
            if (c.Text != text) c.Text = text;
        }

        static void SetText(ToolStripItem c, string text)
        {
            if (c.Text != text) c.Text = text;
        }

        static string F0(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return "—";
            return v.ToString("0", CultureInfo.InvariantCulture);
        }

        static string F1(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "—";
            return v.ToString("0.0", CultureInfo.InvariantCulture);
        }

        static string F2(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return "—";
            return v.ToString("0.00", CultureInfo.InvariantCulture);
        }

        static string FmtHms(double sec)
        {
            if (double.IsNaN(sec) || double.IsInfinity(sec) || sec < 0.0) sec = 0.0;
            long t = (long)sec;
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}",
                t / 3600L, (t / 60L) % 60L, t % 60L);
        }

        static string TailPath(string p, int max)
        {
            if (string.IsNullOrEmpty(p)) return "-";
            if (p.Length <= max) return p;
            return "…" + p.Substring(p.Length - max);
        }

        static string EyeStateText(EyeState s)
        {
            switch (s)
            {
                case EyeState.Open: return "OPEN";
                case EyeState.Closed: return "CLOSED";
                default: return "UNKNOWN";
            }
        }

        static void MapState(SeatState s, out string ko, out Color color)
        {
            switch (s)
            {
                case SeatState.Idle: ko = "대기"; color = ColIdle; break;
                case SeatState.Awake: ko = "공 부 중"; color = ColAwake; break;
                case SeatState.DrowsySuspect: ko = "졸음 의심"; color = ColSuspect; break;
                case SeatState.Drowsy: ko = "졸 음"; color = ColDanger; break;
                case SeatState.AwaySuspect: ko = "이석 의심"; color = ColSuspect; break;
                case SeatState.Away: ko = "이 석"; color = ColDanger; break;
                case SeatState.Unknown: ko = "판정 불가"; color = ColIdle; break;
                default: ko = "-"; color = ColIdle; break;
            }
        }

        static string ReasonKo(UnknownReason r)
        {
            switch (r)
            {
                case UnknownReason.NoFace: return "얼굴 미검출";
                case UnknownReason.SdkError: return "SDK 오류";
                case UnknownReason.LowLandmarkConfidence: return "얼굴 인식 품질 낮음";
                case UnknownReason.PoseOutOfRange: return "고개 각도 벗어남";
                case UnknownReason.EyeOccluded: return "눈 가려짐";
                case UnknownReason.FineOccluded: return "얼굴 가려짐";
                case UnknownReason.AsymmetricEye: return "한쪽 눈만 감김(판정 제외)";
                case UnknownReason.EyelidUnavailable: return "눈 상태 측정 실패";
                default: return "";
            }
        }

        static string TypeKo(AlertType t)
        {
            switch (t)
            {
                case AlertType.Drowsy: return "졸음";
                case AlertType.Away: return "이석";
                case AlertType.Recovered: return "복귀";
                case AlertType.PersonChanged: return "인식대상 변경";
                default: return t.ToString();
            }
        }

        static string LevelKo(AlertLevel l)
        {
            switch (l)
            {
                case AlertLevel.Warn: return "의심";
                case AlertLevel.Alert: return "확정";
                case AlertLevel.Clear: return "해제";
                default: return l.ToString();
            }
        }

        /// <summary>줄바꿈/연속 공백을 한 칸으로 접고 max 길이에서 자른다. 목록 미리보기 전용.</summary>
        static string CompactJson(string json, int max)
        {
            if (string.IsNullOrEmpty(json)) return "";
            StringBuilder sb = new StringBuilder(Math.Min(json.Length, max) + 4);
            bool pendingSpace = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                {
                    pendingSpace = true;
                    continue;
                }
                if (pendingSpace && sb.Length > 0) sb.Append(' ');
                pendingSpace = false;
                sb.Append(c);
                if (sb.Length >= max)
                {
                    sb.Append('…');
                    break;
                }
            }
            return sb.ToString();
        }

        /// <summary>json 문자열이 없을 때 목록/팝업에 보여줄 최소 요약.</summary>
        static string BuildFallbackPreview(AlertEvent a)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "seatId={0} type={1} level={2} durationSec={3:0.0} eventId={4}",
                a.SeatId, a.Type, a.Level, a.DurationSec, a.EventId);
        }
    }
}
