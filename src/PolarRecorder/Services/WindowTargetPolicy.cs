using System;
namespace PolarRecorder.Services;
// Filter window classes, never process names: explorer.exe also owns normal folder windows.
public static class WindowTargetPolicy
{
    public static bool IsShellSurface(string windowClass) =>
        windowClass.Equals("Progman", StringComparison.OrdinalIgnoreCase) ||
        windowClass.Equals("WorkerW", StringComparison.OrdinalIgnoreCase) ||
        windowClass.Equals("Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) ||
        windowClass.Equals("Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase);
    public const string ShellMessage = "바탕화면과 작업 표시줄은 창 녹화 대상으로 선택할 수 없습니다. 바탕화면을 담으려면 전체 화면 또는 선택 영역을 이용하세요.";
    public static string CaptureFailure(Exception error)
    {
        if (error is TimeoutException)
            return "선택한 창에서 미리보기 화면을 받지 못했습니다. 최소화를 해제하고 창을 움직인 뒤 다시 시도하거나 다른 창을 선택하세요.";
        string code = error.Message.Contains("0x80070057", StringComparison.OrdinalIgnoreCase) ? " (0x80070057)" : "";
        return "Windows가 선택한 창의 캡처를 시작하지 못했습니다" + code + ". 창 목록을 새로고침하고 일반 앱 창을 다시 선택하세요. 바탕화면은 전체 화면 모드를 이용하세요. 다른 앱도 실패하면 Windows 창 캡처 및 그래픽 드라이버 지원을 확인하세요.";
    }
}
