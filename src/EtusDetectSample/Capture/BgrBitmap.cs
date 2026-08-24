using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Etus.DetectSample.Capture
{
    /// <summary>
    /// BGR byte[] → <see cref="Bitmap"/>(Format24bppRgb) 변환기.
    ///
    /// Format24bppRgb 는 메모리 배치가 B,G,R 순이라 BGR 버퍼를 그대로 복사하면 된다.
    /// GDI+ 는 각 행을 4바이트 경계에 맞추므로 stride 패딩 보정이 필수다
    /// (참고: FaceSDK.cs:78-107 BlobImg.MakeBitmap 과 같은 로직. 단 여기서는 배열을 복사해 두지 않는다).
    ///
    /// <b>Bitmap 재사용</b> — 프레임마다 new Bitmap 을 만들면 GDI 핸들과 LOH 가 같이 터진다.
    /// 크기가 같으면 기존 인스턴스에 LockBits 로 덮어쓴다.
    ///
    /// <b>다만 인스턴스를 1개만 두면 안 된다.</b> 워커가 만든 Bitmap 을 BeginInvoke 로 UI 에 넘기면
    /// UI 가 그 Bitmap 을 그리는 도중에 워커가 다음 프레임을 같은 Bitmap 에 덮어쓴다
    /// → 화면 tearing, 심하면 GDI+ 의 "개체가 다른 곳에서 사용 중입니다" 예외.
    /// 그래서 <see cref="DefaultBufferCount"/> 개를 돌려 쓴다(기본 2).
    /// 10 FPS(=100ms 간격)에서 UI 한 번 그리는 데 몇 ms 면 2개로 충분하다.
    /// FPS 를 크게 올리거나 UI 가 무거우면 <see cref="BgrBitmap(int)"/> 로 3 이상을 줄 것.
    /// (근본 해결은 아니다 — 완전한 안전을 원하면 프레임마다 새 Bitmap 을 만들어야 한다)
    ///
    /// 스레딩: 워커 스레드 1개만 <see cref="Update"/> 를 호출해야 한다.
    /// </summary>
    public sealed class BgrBitmap : IDisposable
    {
        /// <summary>기본 버퍼 개수.</summary>
        public const int DefaultBufferCount = 2;

        readonly Bitmap[] _pool;

        int _next;
        int _w;
        int _h;
        bool _disposed;

        /// <summary>마지막 <see cref="Update"/> 가 돌려준 Bitmap. 없으면 null.</summary>
        public Bitmap Current { get; private set; }

        /// <summary>현재 Bitmap 폭. 아직 없으면 0.</summary>
        public int Width { get { return _w; } }

        /// <summary>현재 Bitmap 높이. 아직 없으면 0.</summary>
        public int Height { get { return _h; } }

        public BgrBitmap()
            : this(DefaultBufferCount)
        {
        }

        public BgrBitmap(int bufferCount)
        {
            if (bufferCount < 1) bufferCount = 1;
            if (bufferCount > 8) bufferCount = 8;

            _pool = new Bitmap[bufferCount];
        }

        /// <summary>
        /// BGR 버퍼를 Bitmap 으로 옮긴다. 성공하면 풀에서 돌아가며 꺼낸 Bitmap 을 돌려준다.
        /// 입력이 유효하지 않거나 GDI 오류가 나면 null 을 돌려준다(예외를 던지지 않는다).
        ///
        /// 돌려주는 Bitmap 은 <b>이 인스턴스가 소유</b>한다. 호출자가 Dispose 하면 안 된다.
        /// 오래 보관해서도 안 된다 — 버퍼 개수만큼 지나면 덮어쓰인다.
        /// </summary>
        public Bitmap Update(byte[] bgr, int w, int h)
        {
            if (_disposed) return null;
            if (bgr == null || w <= 0 || h <= 0) return null;

            long need = (long)w * h * 3;
            if (bgr.LongLength < need) return null;

            try
            {
                if (_w != w || _h != h)
                {
                    ReleasePool();
                    _w = w;
                    _h = h;
                    _next = 0;
                }

                Bitmap bmp = _pool[_next];
                if (bmp == null)
                {
                    bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
                    _pool[_next] = bmp;
                }

                CopyInto(bmp, bgr, w, h);

                _next = (_next + 1) % _pool.Length;
                Current = bmp;

                return bmp;
            }
            catch (Exception)
            {
                // 미리보기는 없어도 되는 기능이다. 여기서 앱을 죽이지 않는다.
                Current = null;
                return null;
            }
        }

        static void CopyInto(Bitmap bmp, byte[] bgr, int w, int h)
        {
            Rectangle rect = new Rectangle(0, 0, w, h);

            BitmapData bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                int dstStride = bd.Stride;   // GDI+ 는 4바이트 정렬 패딩을 넣는다
                int srcStride = w * 3;
                IntPtr scan0 = bd.Scan0;

                if (dstStride == srcStride)
                {
                    // 폭이 4의 배수면 패딩이 없어 한 번에 복사할 수 있다 (720/1280 은 여기에 해당).
                    Marshal.Copy(bgr, 0, scan0, srcStride * h);
                }
                else
                {
                    for (int y = 0; y < h; y++)
                        Marshal.Copy(bgr, y * srcStride, scan0 + y * dstStride, srcStride);
                }
            }
            finally
            {
                bmp.UnlockBits(bd);
            }
        }

        void ReleasePool()
        {
            Current = null;

            for (int i = 0; i < _pool.Length; i++)
            {
                if (_pool[i] == null) continue;

                try { _pool[i].Dispose(); } catch (Exception) { }
                _pool[i] = null;
            }
        }

        /// <summary>
        /// 풀의 Bitmap 을 전부 해제한다.
        /// UI 가 아직 이 Bitmap 을 그리고 있으면 안 되므로, <b>워커를 먼저 세운 뒤</b>
        /// UI 가 참조를 놓은 다음에 호출할 것.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            ReleasePool();
            _w = 0;
            _h = 0;
        }
    }
}
