using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

namespace Notchy;

// Korean / English strings. Each one is published as an app resource "L.<key>", so XAML using
// {DynamicResource L.Key} (or code using SetResourceReference) updates live when the language changes.
// Text that code sets once uses Loc.T("Key") and refreshes on Loc.Changed.
internal static class Loc
{
    private static readonly Dictionary<string, (string Ko, string En)> Strings = new()
    {
        ["Quit"] = ("Notchy 종료", "Quit Notchy"),
        ["PowerAgain"] = ("한 번 더", "Again"),

        ["NoMedia"] = ("재생 중인 음악 없음", "Nothing playing"),
        ["UnknownTitle"] = ("알 수 없는 제목", "Unknown title"),
        ["Media"] = ("미디어", "Media"),
        ["Mute"] = ("음소거", "Mute"),
        ["CapFull"] = ("전체", "Screen"),
        ["CapFullTip"] = ("화면 전체 캡처 (Win+PrtScn) · 스크린샷 폴더에 저장", "Capture the whole screen (Win+PrtScn) · saved to Screenshots"),
        ["CapRegion"] = ("영역", "Region"),
        ["CapRegionTip"] = ("캡처 도구로 원하는 영역 캡처 (Win+Shift+S)", "Capture an area with Snipping Tool (Win+Shift+S)"),
        ["Record"] = ("녹화", "Record"),
        ["RecordTip"] = ("주 모니터를 MP4로 녹화", "Record the main monitor to MP4"),
        ["CapFullSaved"] = ("화면 전체 저장됨 · 클릭해서 보기", "Screenshot saved · click to show"),
        ["CapFullDone"] = ("화면 전체를 캡처했어요", "Screen captured"),
        ["CapRegionHint"] = ("캡처 도구에서 영역을 고르면 클립보드에 복사돼요", "Pick an area in Snipping Tool; it's copied to the clipboard"),
        ["RecStarting"] = ("준비 중…", "Starting…"),
        ["RecSaving"] = ("저장 중…", "Saving…"),
        ["RecFailed"] = ("녹화 실패: {0}", "Recording failed: {0}"),
        ["RecSaved"] = ("녹화 저장됨 · 클릭해서 보기", "Recording saved · click to show"),

        ["TodoHint"] = ("할 일 추가 (Enter)", "Add a to-do (Enter)"),

        ["Memory"] = ("메모리", "Memory"),
        ["Disk"] = ("디스크", "Disk"),
        ["Battery"] = ("배터리", "Battery"),
        ["Uptime"] = ("가동 시간", "Uptime"),
        ["Threads"] = ("{0}스레드", "{0} threads"),
        ["DiskFree"] = ("{0} 여유 {1:0} GB", "{0} {1:0} GB free"),
        ["Charging"] = ("충전 중", "Charging"),
        ["OnBattery"] = ("배터리 사용 중", "On battery"),
        ["Days"] = ("{0}일", "{0}d"),
        ["Hours"] = ("{0}시간", "{0}h"),
        ["HoursMinutes"] = ("{0}시간 {1}분", "{0}h {1}m"),
        ["Minutes"] = ("{0}분", "{0}m"),

        ["ClipHint"] = ("클릭하면 다시 복사돼요", "Click an item to copy it again"),
        ["ClipCopied"] = ("복사됨 ✓", "Copied ✓"),
        ["ClipClear"] = ("모두 지우기", "Clear all"),
        ["ClipEmpty"] = ("복사한 텍스트가 여기에 쌓여요", "Text you copy shows up here"),

        ["AppsHint"] = ("프로그램, 바로가기, 폴더를 Notchy에 끌어다 놓아도 추가돼요", "Drag programs, shortcuts or folders onto Notchy to add them"),
        ["AppsRemove"] = ("목록에서 빼기", "Remove"),
        ["AppsAdd"] = ("추가", "Add"),
        ["AppsPickTitle"] = ("Notchy에 추가할 프로그램", "Programs to add to Notchy"),
        ["AppsFilter"] = ("프로그램과 바로가기|*.exe;*.lnk;*.url|모든 파일|*.*", "Programs and shortcuts|*.exe;*.lnk;*.url|All files|*.*"),

        ["WpShuffle"] = ("무작위", "Shuffle"),
        ["WpFolder"] = ("폴더 변경", "Change folder"),
        ["WpEmpty"] = ("이 폴더에는 사진이 없어요", "No pictures in this folder"),
        ["WpPickTitle"] = ("배경화면 사진이 있는 폴더", "Folder with wallpaper pictures"),

        ["OptGeneral"] = ("일반", "General"),
        ["OptStartup"] = ("Windows 시작 시 자동 실행", "Start with Windows"),
        ["OptLanguage"] = ("언어", "Language"),
        ["OptLangAuto"] = ("시스템", "System"),
        ["OptBehavior"] = ("동작", "Behavior"),
        ["OptOpenSpeed"] = ("마우스를 올리면 펼치기", "Open on hover"),
        ["OptFast"] = ("바로", "Instantly"),
        ["OptNormal"] = ("보통", "Normal"),
        ["OptSlow"] = ("천천히", "Slowly"),
        ["OptReduceMotion"] = ("애니메이션 줄이기", "Reduce motion"),
        ["OptClock"] = ("시계", "Clock"),
        ["Opt24h"] = ("24시간제", "24-hour clock"),
        ["OptCapture"] = ("캡처", "Capture"),
        ["OptRecordAudio"] = ("녹화에 컴퓨터 소리 포함", "Include system sound in recordings"),
        ["OptHideOwn"] = ("Notchy로 찍을 때 Notchy 숨기기", "Hide Notchy from its own captures"),
        ["OptTabs"] = ("보이는 탭", "Visible tabs"),
        ["OptAbout"] = ("정보", "About"),
        ["OptVersion"] = ("버전 {0}", "Version {0}"),
        ["OptGitHub"] = ("GitHub에서 보기", "View on GitHub"),
        ["OptSettingsFolder"] = ("설정 폴더 열기", "Open settings folder"),
    };

    public static bool IsKorean { get; private set; }
    public static CultureInfo Culture => IsKorean ? CultureInfo.GetCultureInfo("ko-KR") : CultureInfo.GetCultureInfo("en-US");
    public static event Action? Changed;

    // "auto" follows the Windows display language.
    public static void Apply(string language)
    {
        IsKorean = language switch
        {
            "ko" => true,
            "en" => false,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko",
        };
        var resources = Application.Current.Resources;
        foreach (var (key, text) in Strings)
            resources["L." + key] = IsKorean ? text.Ko : text.En;
        Changed?.Invoke();
    }

    public static string T(string key) => Strings.TryGetValue(key, out var s) ? (IsKorean ? s.Ko : s.En) : key;

    public static string T(string key, params object[] args) => string.Format(Culture, T(key), args);
}
