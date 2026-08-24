using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using OpenCvSharp;

namespace Etoos.DetectSample.Capture
{
    /// <summary>
    /// OpenCvSharp VideoCapture 래퍼 + 분석 입력 전처리 (CONTRACT.md 부록 A-7).
    ///
    /// SDK 에 넘기는 버퍼는 캡처 원본이 아니라 <b>AnalyzeWidth x AnalyzeHeight(기본 720x1280 세로)</b> 다.
    /// <code>
    ///   VideoCapture (보통 1280x720 가로)
    ///      ↓ 좌우반전   FlipHorizontal      ← 거울상 보정이 먼저다
    ///      ↓ 회전       RotateDegrees       (0/90/180/270)
    ///      ↓ 중앙 크롭  EnableAspectCrop    (AnalyzeWidth:AnalyzeHeight 종횡비)
    ///      ↓ 리사이즈   EnableAnalyzeResize (→ AnalyzeWidth x AnalyzeHeight)
    ///   GetBgr() / GetAnalyzeMat()
    /// </code>
    ///
    /// <b>좌표계 계약</b> — <c>FrameObservation</c> 의 box/landmark 는 전부 <b>분석 이미지 좌표</b>다.
    /// 그러므로 화면 프리뷰도 반드시 이 분석 이미지로 그려야 오버레이가 맞는다.
    /// 캡처 원본을 프리뷰로 쓰면 회전/크롭/스케일만큼 어긋난다.
    /// (<see cref="GetBgr"/> 과 <see cref="GetAnalyzeMat"/> 는 같은 이미지를 각각 byte[] / Mat 으로 준다)
    ///
    /// 전처리는 모두 <b>Mat 단계</b>에서 한다. byte[] 로 뽑은 뒤에 하면 느리다.
    ///
    /// 스레딩: 스레드 안전하지 않다. 워커 스레드 1개가 독점 소유한다(CONTRACT.md 3장).
    ///
    /// 할당 정책: 중간 Mat 과 출력 byte[] 를 전부 재사용한다.
    ///   1280x720 BGR 한 장이 2.7MB 라 프레임마다 새로 잡으면 곧바로 LOH 로 가서 GC 가 요동친다.
    /// </summary>
    public sealed class CameraCapture : IDisposable
    {
        VideoCapture _capture;

        // --- 재사용 Mat ---
        Mat _raw;         // 카메라 원본
        Mat _flipTmp;     // 좌우반전 더블버퍼 (포인터 swap 용)
        Mat _rotated;     // 회전 결과
        Mat _resized;     // 리사이즈 결과
        Mat _continuous;  // 크롭만 하고 리사이즈는 안 할 때, ROI 를 연속 버퍼로 복사할 곳

        // 마지막 Read() 의 최종 결과. 위 Mat 들 중 하나를 '가리키기만' 한다. 소유하지 않는다.
        Mat _current;

        // --- 재사용 출력 버퍼 ---
        byte[] _bgrBuf;

        bool _disposed;

        /// <summary>VideoCapture 백엔드. ANY / MSMF / DSHOW / FFMPEG. Open() 전에 설정할 것.</summary>
        public string ApiPreference { get; set; }

        /// <summary>거울 모드. 판정에는 영향이 없다(눈꺼풀 거리는 좌우 대칭).</summary>
        public bool FlipHorizontal { get; set; }

        /// <summary>캡처 직후 회전각. 0 / 90 / 180 / 270 만 유효. 90 은 시계방향.</summary>
        public int RotateDegrees { get; set; }

        /// <summary>회전 후 AnalyzeWidth:AnalyzeHeight 종횡비로 중앙 크롭할지.</summary>
        public bool EnableAspectCrop { get; set; }

        /// <summary>크롭 결과를 AnalyzeWidth x AnalyzeHeight 로 리사이즈할지.</summary>
        public bool EnableAnalyzeResize { get; set; }

        /// <summary>SDK 에 넘길 목표 폭.</summary>
        public int AnalyzeWidth { get; set; }

        /// <summary>SDK 에 넘길 목표 높이.</summary>
        public int AnalyzeHeight { get; set; }

        public bool IsOpen
        {
            get { return _capture != null && _capture.IsOpened(); }
        }

        /// <summary>카메라가 '실제로' 열어 준 캡처 폭. 요청값과 다를 수 있다.</summary>
        public int FrameWidth { get; private set; }

        /// <summary>카메라가 '실제로' 열어 준 캡처 높이. 요청값과 다를 수 있다.</summary>
        public int FrameHeight { get; private set; }

        /// <summary>마지막 Read() 의 전처리 결과 폭. FrameObservation 의 좌표계는 이 크기 기준이다.</summary>
        public int AnalyzeFrameWidth { get; private set; }

        /// <summary>마지막 Read() 의 전처리 결과 높이.</summary>
        public int AnalyzeFrameHeight { get; private set; }

        /// <summary>실제로 열린 백엔드 이름(요청과 다를 수 있으므로 UI 표시용).</summary>
        public string OpenedApiName { get; private set; }

        public CameraCapture()
        {
            ApiPreference = "ANY";
            FlipHorizontal = true;
            RotateDegrees = 0;
            EnableAspectCrop = true;
            EnableAnalyzeResize = true;
            AnalyzeWidth = 720;
            AnalyzeHeight = 1280;
        }

        //
        // Open / Close
        //

        /// <summary>
        /// 카메라를 연다. 여는 데 성공해도 프레임이 안 나오는 카메라가 있으므로
        /// 참고 샘플과 동일하게 <b>테스트 캡처를 1회</b> 해서 실제 동작을 확인한다.
        /// </summary>
        /// <param name="index">카메라 인덱스</param>
        /// <param name="width">요청 폭 (카메라가 거부할 수 있다)</param>
        /// <param name="height">요청 높이</param>
        /// <param name="fourcc">MJPG 등. 빈 문자열이면 설정하지 않는다.</param>
        /// <param name="error">실패 사유(한국어). 성공 시 빈 문자열.</param>
        public bool Open(int index, int width, int height, string fourcc, out string error)
        {
            error = "";

            if (_disposed)
            {
                error = "이미 Dispose 된 CameraCapture 입니다.";
                return false;
            }

            Close();

            VideoCaptureAPIs api = ResolveApi(ApiPreference);
            VideoCapture capture = null;

            try
            {
                capture = new VideoCapture(index, api);

                if (!capture.IsOpened())
                {
                    error = "카메라를 열지 못했습니다 (index=" + index + ", api=" + api + ")." +
                            Environment.NewLine +
                            "다른 프로그램이 카메라를 쓰고 있는지, 인덱스가 맞는지 확인하세요.";
                    capture.Dispose();
                    return false;
                }

                if (!string.IsNullOrEmpty(fourcc))
                {
                    // MJPG 로 열리지 않으면 카메라가 조용히 무시한다. 실패해도 진행한다.
                    try
                    {
                        // FourCC 는 struct 다. int 로 명시 변환해서 Set(.., double) 오버로드에 넘긴다.
                        int code = FourCC.FromString(fourcc);
                        capture.Set(VideoCaptureProperties.FourCC, code);
                    }
                    catch (Exception) { }
                }

                capture.Set(VideoCaptureProperties.FrameWidth, width);
                capture.Set(VideoCaptureProperties.FrameHeight, height);

                // 테스트 캡처 1회 — 열리기만 하고 프레임이 안 나오는 조합이 흔하다.
                using (Mat probe = new Mat())
                {
                    if (!capture.Read(probe) || probe.Empty())
                    {
                        error = "카메라는 열렸지만 프레임을 받지 못했습니다 (index=" + index + ", api=" + api + ")." +
                                Environment.NewLine +
                                "1) 카메라 인덱스를 바꿔보세요." + Environment.NewLine +
                                "2) CaptureApi 를 ANY / MSMF / DSHOW / FFMPEG 로 바꿔보세요.";
                        capture.Release();
                        capture.Dispose();
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                if (capture != null)
                {
                    try { capture.Release(); } catch (Exception) { }
                    capture.Dispose();
                }
                error = "카메라 열기 중 예외: " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }

            _capture = capture;
            OpenedApiName = api.ToString();

            // 요청값이 아니라 '실제 적용된' 크기를 보관한다.
            FrameWidth = _capture.FrameWidth;
            FrameHeight = _capture.FrameHeight;

            AnalyzeFrameWidth = 0;
            AnalyzeFrameHeight = 0;

            return true;
        }

        public void Close()
        {
            _current = null;

            DisposeMat(ref _continuous);
            DisposeMat(ref _resized);
            DisposeMat(ref _rotated);
            DisposeMat(ref _flipTmp);
            DisposeMat(ref _raw);

            if (_capture == null) return;

            try { _capture.Release(); } catch (Exception) { }
            _capture.Dispose();
            _capture = null;
        }

        //
        // Read
        //

        /// <summary>
        /// 한 프레임 읽고 전처리까지 끝낸다. 성공하면 <see cref="GetBgr"/> 로 버퍼를 꺼낼 수 있다.
        /// 프레임을 못 받으면 false 를 돌려준다(예외를 던지지 않는다).
        /// </summary>
        public bool Read()
        {
            if (!IsOpen) return false;

            try
            {
                if (_raw == null) _raw = new Mat();

                if (!_capture.Read(_raw) || _raw.Empty())
                    return false;

                Mat src = _raw;

                // --- 좌우 반전: 새 Mat 을 만들지 않고 더블버퍼 포인터 swap ---
                if (FlipHorizontal)
                {
                    if (_flipTmp == null) _flipTmp = new Mat();

                    Cv2.Flip(_raw, _flipTmp, FlipMode.Y);

                    Mat swap = _raw;
                    _raw = _flipTmp;
                    _flipTmp = swap;

                    src = _raw;
                }

                // --- 회전 ---
                int rot = NormalizeRotation(RotateDegrees);
                if (rot != 0)
                {
                    if (_rotated == null) _rotated = new Mat();

                    Cv2.Rotate(src, _rotated, ToRotateFlag(rot));
                    src = _rotated;
                }

                // --- 종횡비 중앙 크롭 ---
                Mat roi = null;
                Mat cropSrc = src;

                if (EnableAspectCrop && AnalyzeWidth > 0 && AnalyzeHeight > 0)
                {
                    Rect r = ComputeCenterCrop(src.Width, src.Height, AnalyzeWidth, AnalyzeHeight);
                    if (r.Width != src.Width || r.Height != src.Height)
                    {
                        // Mat header 만 만드는 view. 픽셀 복사는 없다. 대신 IsContinuous()==false 가 된다.
                        roi = new Mat(src, r);
                        cropSrc = roi;
                    }
                }

                try
                {
                    if (EnableAnalyzeResize && AnalyzeWidth > 0 && AnalyzeHeight > 0 &&
                        (cropSrc.Width != AnalyzeWidth || cropSrc.Height != AnalyzeHeight))
                    {
                        if (_resized == null) _resized = new Mat();

                        // 보간은 축소/확대에 따라 다르게 골라야 한다.
                        //  - 축소: Area 가 모아레를 억제하고 속도도 빠르다.
                        //  - 확대: Area 는 최근접 보간처럼 동작해 계단현상이 생긴다 → Linear.
                        //
                        // 확대는 흔한 경우다. 예) 1280x720 가로캠 + SrcRotateDegrees=0 이면
                        // 720:1280 종횡비 중앙 크롭 결과가 405x720 이라, 720x1280 으로 가려면 1.78배 확대다.
                        // 여기서 Area 를 쓰면 landmark 정확도가 눈에 띄게 나빠진다.
                        bool shrinking = AnalyzeWidth < cropSrc.Width || AnalyzeHeight < cropSrc.Height;

                        Cv2.Resize(cropSrc, _resized, new Size(AnalyzeWidth, AnalyzeHeight),
                                   0, 0, shrinking ? InterpolationFlags.Area : InterpolationFlags.Linear);
                        _current = _resized;
                    }
                    else if (roi != null)
                    {
                        // EnableAnalyzeResize=false 경로(또는 이미 목표 크기인 경우).
                        //
                        // ROI 는 원본의 stride 를 물려받으므로 IsContinuous()==false 다.
                        // 그대로 byte[] 로 뽑으면 행 사이 padding 이 섞여 SDK 가 이미지를 어긋나게 읽는다.
                        // → 반드시 연속 버퍼로 한 번 복사한다.
                        //
                        // Clone() 이 아니라 CopyTo(_continuous) 를 쓴다.
                        // Clone() 은 프레임마다 새 Mat 을 만들지만, CopyTo 는 크기가 같으면
                        // 기존 버퍼를 재사용한다(할당 정책 참조).
                        if (_continuous == null) _continuous = new Mat();

                        roi.CopyTo(_continuous);
                        _current = _continuous;
                    }
                    else
                    {
                        // 크롭도 리사이즈도 없었다 → _raw / _rotated 를 그대로 쓴다. 둘 다 연속 버퍼다.
                        _current = cropSrc;
                    }
                }
                finally
                {
                    if (roi != null) roi.Dispose();
                }

                AnalyzeFrameWidth = _current.Width;
                AnalyzeFrameHeight = _current.Height;

                return true;
            }
            catch (Exception)
            {
                // 카메라가 뽑히는 등의 상황. 워커가 재시도할 수 있게 false 만 돌려준다.
                _current = null;
                return false;
            }
        }

        /// <summary>
        /// 마지막 <see cref="Read"/> 의 <b>분석 이미지 Mat</b> 을 돌려준다(<see cref="GetBgr"/> 과 동일한 이미지).
        /// 프리뷰를 Mat 단계에서 바로 쓰고 싶을 때(오버레이 그리기 등) 사용한다.
        ///
        /// <b>이 인스턴스가 소유한다.</b> 호출자가 Dispose 하면 안 되고, 보관해서도 안 된다 —
        /// 다음 <see cref="Read"/> 가 같은 Mat 을 덮어쓴다. 보관하려면 직접 Clone() 할 것.
        ///
        /// 아직 Read() 를 못 했거나 실패했으면 null.
        /// </summary>
        public Mat GetAnalyzeMat()
        {
            Mat m = _current;

            if (m == null) return null;
            if (m.IsDisposed) return null;

            return m;
        }

        /// <summary>
        /// 마지막 <see cref="Read"/> 결과를 연속 BGR byte[] 로 돌려준다.
        /// 캡처 원본이 아니라 <b>전처리가 끝난 분석 이미지</b>(기본 720x1280)다.
        /// 크기는 <see cref="AnalyzeFrameWidth"/> / <see cref="AnalyzeFrameHeight"/> 로 확인한다.
        ///
        /// <b>돌려주는 배열은 매 프레임 재사용된다.</b> 호출자는 이 배열을 보관하면 안 된다
        /// (다음 Read() 가 같은 메모리를 덮어쓴다). SDK 호출처럼 동기적으로 소비하는 용도.
        ///
        /// 가드 4종(null / 3채널 / CV_8UC3 / IsContinuous)을 통과하지 못하면 null.
        /// </summary>
        public byte[] GetBgr()
        {
            Mat m = _current;

            if (m == null) return null;
            if (m.IsDisposed) return null;
            if (m.Channels() != 3) return null;                 // BGR 만
            if (m.Type() != MatType.CV_8UC3) return null;
            if (!m.IsContinuous()) return null;                 // 행 사이 padding 이 있으면 그대로 못 넘긴다

            long total = (long)m.Total() * m.ElemSize();
            if (total <= 0 || total > int.MaxValue) return null;

            int bytes = (int)total;

            if (_bgrBuf == null || _bgrBuf.Length != bytes)
                _bgrBuf = new byte[bytes];

            Marshal.Copy(m.Data, _bgrBuf, 0, bytes);

            return _bgrBuf;
        }

        //
        // helpers
        //

        /// <summary>
        /// src 를 targetW:targetH 종횡비로 중앙 크롭할 사각형을 구한다.
        /// 결과는 항상 src 안쪽이며 폭/높이가 1 이상이다.
        /// </summary>
        internal static Rect ComputeCenterCrop(int srcW, int srcH, int targetW, int targetH)
        {
            if (srcW <= 0 || srcH <= 0 || targetW <= 0 || targetH <= 0)
                return new Rect(0, 0, Math.Max(1, srcW), Math.Max(1, srcH));

            double srcAspect = (double)srcW / srcH;
            double dstAspect = (double)targetW / targetH;

            int cw, ch;

            if (srcAspect > dstAspect)
            {
                // 원본이 더 넓다 → 좌우를 잘라낸다.
                ch = srcH;
                cw = (int)Math.Round(srcH * dstAspect);
            }
            else
            {
                // 원본이 더 높다 → 위아래를 잘라낸다.
                cw = srcW;
                ch = (int)Math.Round(srcW / dstAspect);
            }

            if (cw < 1) cw = 1;
            if (ch < 1) ch = 1;
            if (cw > srcW) cw = srcW;
            if (ch > srcH) ch = srcH;

            int x = (srcW - cw) / 2;
            int y = (srcH - ch) / 2;

            return new Rect(x, y, cw, ch);
        }

        internal static int NormalizeRotation(int degrees)
        {
            int d = ((degrees % 360) + 360) % 360;
            if (d == 90 || d == 180 || d == 270) return d;
            return 0;
        }

        static RotateFlags ToRotateFlag(int normalizedDegrees)
        {
            if (normalizedDegrees == 90) return RotateFlags.Rotate90Clockwise;
            if (normalizedDegrees == 180) return RotateFlags.Rotate180;
            return RotateFlags.Rotate90Counterclockwise;   // 270
        }

        static VideoCaptureAPIs ResolveApi(string name)
        {
            if (string.IsNullOrEmpty(name)) return VideoCaptureAPIs.ANY;

            string n = name.Trim().ToUpperInvariant();

            if (n == "MSMF") return VideoCaptureAPIs.MSMF;
            if (n == "DSHOW") return VideoCaptureAPIs.DSHOW;
            if (n == "FFMPEG") return VideoCaptureAPIs.FFMPEG;

            return VideoCaptureAPIs.ANY;
        }

        static void DisposeMat(ref Mat m)
        {
            if (m == null) return;
            try { m.Dispose(); } catch (Exception) { }
            m = null;
        }

        //
        // 카메라 목록
        //

        /// <summary>
        /// index 0..maxProbe-1 을 실제로 열어보고 성공한 것만 돌려준다.
        ///
        /// 항목 형식은 "<c>0 - 1280x720</c>" 이다. 앞의 정수가 카메라 인덱스이며
        /// <see cref="ParseCameraIndex"/> 로 다시 뽑을 수 있다.
        ///
        /// <b>느리다.</b> 인덱스 하나당 수백 ms ~ 수 초가 걸린다(백엔드가 장치를 실제로 연다).
        /// UI 스레드에서 부르면 창이 멈춘다. 그래서 maxProbe 기본값을 4 로 낮게 뒀다.
        ///
        /// <b>주의:</b> 워커가 이미 쓰고 있는 인덱스는 대부분 열리지 않아 목록에서 빠진다.
        ///        카메라를 멈춘 상태에서 부를 것.
        /// </summary>
        public static string[] EnumerateCameras(int maxProbe = 4)
        {
            if (maxProbe < 1) maxProbe = 1;
            if (maxProbe > 16) maxProbe = 16;

            List<string> found = new List<string>();

            for (int i = 0; i < maxProbe; i++)
            {
                VideoCapture cap = null;
                try
                {
                    cap = new VideoCapture(i);

                    if (cap.IsOpened())
                    {
                        found.Add(i.ToString(CultureInfo.InvariantCulture) +
                                  " - " + cap.FrameWidth + "x" + cap.FrameHeight);
                    }
                }
                catch (Exception)
                {
                    // 없는 인덱스는 백엔드에 따라 예외를 던지기도 한다. 그냥 건너뛴다.
                }
                finally
                {
                    if (cap != null)
                    {
                        // 다음 인덱스 탐색 전에 즉시 장치를 놓아준다.
                        try { cap.Release(); } catch (Exception) { }
                        try { cap.Dispose(); } catch (Exception) { }
                    }
                }
            }

            return found.ToArray();
        }

        /// <summary>EnumerateCameras 항목에서 카메라 인덱스를 뽑는다. 실패하면 -1.</summary>
        public static int ParseCameraIndex(string item)
        {
            if (string.IsNullOrEmpty(item)) return -1;

            int end = 0;
            while (end < item.Length && char.IsDigit(item[end])) end++;

            if (end == 0) return -1;

            int idx;
            if (!int.TryParse(item.Substring(0, end), NumberStyles.Integer, CultureInfo.InvariantCulture, out idx))
                return -1;

            return idx;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Close();
            _bgrBuf = null;
        }
    }
}
