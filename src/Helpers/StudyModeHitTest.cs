namespace OhSubtitle.Helpers;

internal enum StudyModeCursorArea
{
    Outside,
    Drag,
    Reveal
}

internal static class StudyModeHitTest
{
    // 坐标均为屏幕像素；窗口左上包含在内，右下不包含在内。
    internal static StudyModeCursorArea Classify(int left, int top, int right, int bottom, int x, int y)
    {
        if (x < left || x >= right || y < top || y >= bottom)
        {
            return StudyModeCursorArea.Outside;
        }

        long width = (long)right - left;
        long offset = (long)x - left;
        return 5 * offset >= width && 5 * offset < 4 * width
            ? StudyModeCursorArea.Reveal
            : StudyModeCursorArea.Drag;
    }
}
