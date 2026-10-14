using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

namespace Brim;

// Korean / English strings. Each one is published as an app resource "L.<key>", so XAML using
// {DynamicResource L.Key} (or code using SetResourceReference) updates live when the language changes.
// Text that code sets once uses Loc.T("Key") and refreshes on Loc.Changed.
internal static class Loc
{
    private static readonly Dictionary<string, (string Ko, string En)> Strings = new()
    {
        ["Quit"] = ("Brim 종료", "Quit Brim"),
        ["PowerAgain"] = ("한 번 더", "Again"),
        ["SchedAction"] = ("동작", "Action"),
        ["SchedAfter"] = ("다음 시간 뒤에", "After"),
        ["SchedAt"] = ("시각", "At"),
        ["SchedSet"] = ("예약", "Set"),
        ["SchedBack"] = ("‹ 뒤로", "‹ Back"),
        ["Sched15"] = ("15분", "15 min"),
        ["Sched30"] = ("30분", "30 min"),
        ["Sched60"] = ("1시간", "1 hr"),
        ["Sched120"] = ("2시간", "2 hr"),
        ["ActShutdown"] = ("종료", "Shut down"),
        ["ActRestart"] = ("재시작", "Restart"),
        ["ActSleep"] = ("절전", "Sleep"),
        ["SchedStatus"] = ("{0} 후 {1} ({2})", "{1} in {0} (at {2})"),
        ["SchedCancel"] = ("예약 취소", "Cancel"),
        ["SchedBadTime"] = ("23:30처럼 적어 주세요", "Use a time like 23:30"),
        ["SchedMissed"] = ("예약 시각이 지나 있어서 실행하지 않고 취소했어요", "The scheduled time had passed, so it was canceled instead"),

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
        ["ClipTexts"] = ("텍스트 {0}", "Text {0}"),
        ["ClipImages"] = ("사진 {0}", "Images {0}"),
        ["ClipEmptyTexts"] = ("복사한 텍스트가 여기에 쌓여요", "Text you copy shows up here"),
        ["ClipEmptyImages"] = ("복사한 사진이 여기에 쌓여요 (최근 10장)", "Images you copy show up here (last 10)"),
        ["ClipImage"] = ("이미지 · {0}×{1}", "Image · {0}×{1}"),

        ["AppsAll"] = ("전체", "All"),
        ["AppsGroupName"] = ("그룹 이름", "Group name"),
        ["AppsRename"] = ("이름 바꾸기", "Rename"),
        ["AppsDeleteGroup"] = ("그룹 삭제 (앱은 남아요)", "Delete group (apps stay)"),
        ["AppsMoveTo"] = ("그룹", "Group"),
        ["AppsNoGroup"] = ("그룹 없음", "No group"),
        ["AppsNewGroup"] = ("새 그룹", "New group"),
        ["AppsGroupEmpty"] = ("비어 있어요 · 앱을 오른쪽 클릭해서 이 그룹으로 옮기거나, 끌어다 놓으세요", "Empty · right-click an app to move it here, or drop files onto Brim"),

        ["TopCpu"] = ("CPU를 많이 쓰는 앱", "Top apps by CPU"),
        ["TopMemory"] = ("메모리를 많이 쓰는 앱", "Top apps by memory"),
        ["TopMeasuring"] = ("측정 중…", "Measuring…"),
        ["StatsPick"] = ("CPU나 메모리 칸을 눌러 바꿀 수 있어요", "Click CPU or Memory to switch"),

        ["WinEmpty"] = ("열린 창이 없어요", "No open windows"),
        ["WinClose"] = ("창 닫기", "Close window"),
        ["WinDesktop"] = ("바탕 화면", "Desktop"),
        ["WinTaskView"] = ("작업 보기", "Task view"),
        ["WinQuick"] = ("빠른 설정", "Quick settings"),
        ["WinNotify"] = ("알림", "Notifications"),

        ["Timer"] = ("타이머", "Timer"),
        ["TimerDone"] = ("시간이 다 됐어요", "Time's up"),

        ["AppsHint"] = ("프로그램, 바로가기, 폴더를 Brim에 끌어다 놓아도 추가돼요", "Drag programs, shortcuts or folders onto Brim to add them"),
        ["AppsRemove"] = ("목록에서 빼기", "Remove"),
        ["AppsAdd"] = ("추가", "Add"),
        ["AppsPickTitle"] = ("Brim에 추가할 프로그램", "Programs to add to Brim"),
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
        ["OptHideOwn"] = ("Brim으로 찍을 때 Brim 숨기기", "Hide Brim from its own captures"),
        ["OptTabs"] = ("보이는 탭", "Visible tabs"),
        ["OptUpdates"] = ("업데이트", "Updates"),
        ["UpdAuto"] = ("업데이트 자동 확인", "Check for updates automatically"),
        ["UpdCheckNow"] = ("지금 확인", "Check now"),
        ["UpdRestart"] = ("재시작해서 업데이트", "Restart to update"),
        ["UpdIdle"] = ("아직 확인하지 않았어요", "Not checked yet"),
        ["UpdChecking"] = ("확인 중…", "Checking…"),
        ["UpdLatest"] = ("최신 버전이에요 ({0})", "Up to date ({0})"),
        ["UpdDownloading"] = ("새 버전 {0} 받는 중…", "Downloading {0}…"),
        ["UpdReady"] = ("새 버전 {0} 준비됨 · 다음 실행 때 적용돼요", "Version {0} is ready · applies on next launch"),
        ["UpdFailed"] = ("업데이트를 확인하지 못했어요", "Couldn't check for updates"),
        ["UpdDev"] = ("개발용 빌드라 자동 업데이트를 쓰지 않아요", "Development build: updates are off"),
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
