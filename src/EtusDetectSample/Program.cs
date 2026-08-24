using System;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Etus.DetectSample
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // 데모 도중 아무 메시지 없이 앱이 사라지는 것이 가장 나쁜 시나리오다.
            // UI 스레드 예외와 그 외 스레드(워커 포함)의 미처리 예외를 모두 잡아 스택을 보여준다.
            //
            // SetUnhandledExceptionMode 는 첫 창이 만들어지기 전에 호출해야 한다.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnUiThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                // 배선은 AppController 한 곳에서만 한다(MainForm 은 순수 뷰).
                using (MainForm form = new MainForm())
                using (AppController controller = new AppController(form))
                {
                    // 카메라 열거는 장치당 수 초가 걸릴 수 있다. 폼이 먼저 뜨게 한 뒤 채운다.
                    form.Shown += delegate { controller.Prepare(); };
                    Application.Run(form);
                }
            }
            catch (Exception ex)
            {
                ShowFatal("앱 시작 중 오류가 발생했습니다", ex);
            }
        }

        static void OnUiThreadException(object sender, ThreadExceptionEventArgs e)
        {
            ShowFatal("UI 스레드에서 처리되지 않은 예외가 발생했습니다", e.Exception);
        }

        static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // AppDomain 쪽은 Exception 이 아닐 수도 있다.
            Exception ex = e.ExceptionObject as Exception;

            if (ex != null)
                ShowFatal("처리되지 않은 예외가 발생했습니다", ex);
            else
                ShowFatalText("처리되지 않은 예외가 발생했습니다",
                              e.ExceptionObject == null ? "(알 수 없음)" : e.ExceptionObject.ToString());
        }

        static void ShowFatal(string title, Exception ex)
        {
            StringBuilder sb = new StringBuilder();

            Exception cur = ex;
            int depth = 0;

            while (cur != null && depth < 5)
            {
                if (depth > 0) sb.AppendLine().AppendLine("--- 내부 예외 ---");

                sb.AppendLine(cur.GetType().FullName);
                sb.AppendLine(cur.Message);
                sb.AppendLine();
                sb.AppendLine(cur.StackTrace);

                cur = cur.InnerException;
                depth++;
            }

            ShowFatalText(title, sb.ToString());
        }

        static void ShowFatalText(string title, string detail)
        {
            try
            {
                MessageBox.Show(detail, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception)
            {
                // 메시지 박스조차 못 띄우는 상황(세션 종료 중 등). 여기서 더 할 수 있는 게 없다.
            }
        }
    }
}
