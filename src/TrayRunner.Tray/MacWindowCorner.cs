using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TrayRunner.Tray;

/// <summary>
/// 以 Objective-C runtime 互操作,修整 macOS 無邊框毛玻璃視窗的外觀:對 Avalonia 為
/// AcrylicBlur 插入的 NSVisualEffectView 設定 always-active 狀態(失焦不泛白)與自身圓角裁切,
/// 並把 NSWindow 背景改為 clearColor(無邊框視窗預設黑底,圓角裁出後四角會露黑)。
/// 注意:這依賴 Avalonia macOS backend(AvnWindow/AutoFitContentView)的私有 view 階層,
/// 升級 Avalonia 後若面板四角又出現底版,需重新檢視此處假設。
/// </summary>
[SupportedOSPlatform("macos")]
internal static class MacWindowCorner
{
    private const string Objc = "/usr/lib/libobjc.A.dylib";

    [DllImport(Objc)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendPtr(IntPtr receiver, IntPtr selector);

    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    private static extern void SendVoid(IntPtr receiver, IntPtr selector);

    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    private static extern void SendBool(IntPtr receiver, IntPtr selector, byte value);

    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    private static extern void SendDouble(IntPtr receiver, IntPtr selector, double value);

    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    private static extern nuint SendUInt(IntPtr receiver, IntPtr selector);

    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendPtrIndex(IntPtr receiver, IntPtr selector, nuint index);

    [DllImport(Objc)]
    private static extern IntPtr object_getClassName(IntPtr obj);

    [DllImport(Objc)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    private static extern void SendPtrArg(IntPtr receiver, IntPtr selector, IntPtr arg);

    private static string ClassOf(IntPtr obj) =>
        obj == IntPtr.Zero ? "(null)" : Marshal.PtrToStringAnsi(object_getClassName(obj)) ?? "?";

    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    private static extern void SendNInt(IntPtr receiver, IntPtr selector, nint value);

    // NSVisualEffectState.Active:固定以「作用中」外觀繪製,不隨視窗失焦泛白。
    private const nint NSVisualEffectStateActive = 1;

    /// <summary>
    /// 把承載 AcrylicBlur 的 NSVisualEffectView 本身做兩件事,消除失焦反白與方形底版:
    /// (1) <c>state = Active</c> 讓毛玻璃失焦時不切換成泛白外觀;
    /// (2) 在其自身 layer 設 <paramref name="radius"/> 圓角並 <c>masksToBounds</c> 裁切。
    /// 刻意不動父層 contentView——裁父層會連帶裁掉 Skia 繪製層(AvnView)而露出黑色底襯,
    /// 且 NSVisualEffectView 的模糊由 window server 合成,父層裁切對它無效。
    /// <paramref name="nsWindow"/> 為 <c>TryGetPlatformHandle</c> 回傳的 AvnWindow。
    /// 需在視窗已顯示後呼叫。
    /// </summary>
    public static void Round(IntPtr nsWindow, double radius)
    {
        if (nsWindow == IntPtr.Zero)
            return;

        // 無邊框 NSWindow 預設背景為黑色;毛玻璃裁圓角後四角會露出這層黑底,
        // 改設 clearColor 讓角落透出桌面。
        var clear = SendPtr(objc_getClass("NSColor"), sel_registerName("clearColor"));
        if (clear != IntPtr.Zero)
            SendPtrArg(nsWindow, sel_registerName("setBackgroundColor:"), clear);
        SendBool(nsWindow, sel_registerName("setOpaque:"), 0);

        var contentView = SendPtr(nsWindow, sel_registerName("contentView"));
        ApplyToEffectViews(contentView, radius);

        // 圓角後請 NSWindow 重畫陰影,避免陰影仍是原本的方形輪廓。
        SendVoid(nsWindow, sel_registerName("invalidateShadow"));
    }

    // 遞迴尋訪 view 樹,對每個 NSVisualEffectView 設 Active 狀態與圓角裁切。
    private static void ApplyToEffectViews(IntPtr view, double radius)
    {
        if (view == IntPtr.Zero)
            return;

        if (ClassOf(view) == "NSVisualEffectView")
        {
            SendNInt(view, sel_registerName("setState:"), NSVisualEffectStateActive);

            var layer = SendPtr(view, sel_registerName("layer"));
            if (layer != IntPtr.Zero)
            {
                // CGFloat 在 64 位上即 double;BOOL 以 byte(1/0)傳遞。
                SendDouble(layer, sel_registerName("setCornerRadius:"), radius);
                SendBool(layer, sel_registerName("setMasksToBounds:"), 1);
            }
        }

        var subs = SendPtr(view, sel_registerName("subviews"));
        if (subs == IntPtr.Zero)
            return;
        var count = SendUInt(subs, sel_registerName("count"));
        for (nuint i = 0; i < count; i++)
            ApplyToEffectViews(SendPtrIndex(subs, sel_registerName("objectAtIndex:"), i), radius);
    }
}
