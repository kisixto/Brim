# Brim

Windows 화면 맨 위에 숨어 있는 작은 노치 패널이에요. 평소에는 얇은 선 하나만 보이고, 마우스를 화면 위쪽 가운데로 올리면 부드럽게 펼쳐져요.

*[English below](#english)*

## 기능

| 탭 | 하는 일 |
|---|---|
| **Controls** | 지금 재생 중인 음악/영상(앨범 아트, 이전·재생·다음), 여러 앱에서 재생 중이면 골라서 조작, 볼륨, 노트북 밝기, 화면 캡처(전체 · 캡처 도구로 영역), 화면 녹화(MP4, 컴퓨터 소리 포함) |
| **Today** | 날짜와 초 단위 시계, 할 일 목록 |
| **Stats** | CPU, 메모리, 디스크, 배터리(또는 가동 시간) |
| **Clipboard** | 최근 복사한 텍스트 30개. 클릭하면 다시 복사. 파일로 저장하지 않고, 비밀번호 관리자가 복사한 내용은 기록하지 않아요 |
| **Apps** | 자주 쓰는 프로그램·폴더. 아일랜드에 파일을 끌어다 놓으면 추가 |
| **Wallpaper** | 폴더의 사진을 골라 바로 배경화면으로 |
| **Power** | 잠금, 절전, 재시작, 종료 (재시작·종료는 3초 안에 한 번 더 눌러야 실행) |
| **Options** | Windows 시작 시 자동 실행, 언어(한국어/English), 펼치는 속도, 애니메이션 줄이기, 12/24시간제, 녹화 소리, 보이는 탭, 자동 업데이트 |

## 설치

[Releases](https://github.com/kisixto/Brim/releases/latest)에서 둘 중 하나를 받아요.

- **`Brim-win-Setup.exe` (추천)**: 실행하면 바로 설치되고 시작 메뉴와 바탕화면에 등록돼요. 필요하면 .NET 8과 Visual C++ 런타임도 자동으로 설치해요. 지우려면 Windows 설정 → 앱에서 제거하면 돼요.
  - 기본 위치는 `%LocalAppData%\Brim`이에요. 다른 곳에 설치하려면: `Brim-win-Setup.exe --installto "D:\Apps\Brim"`
- **`Brim-win-Portable.zip`**: 원하는 폴더(다른 드라이브, USB 등)에 압축을 풀고 `Brim.exe`를 실행해요. 설치하지 않아요. PC에 [.NET 8 데스크톱 런타임](https://dotnet.microsoft.com/download/dotnet/8.0)이 없으면 처음 실행할 때 설치하라는 안내가 떠요.

화면 위쪽 가운데에 얇은 검은 선이 생기면 성공이에요. Windows를 켤 때마다 실행하려면 **Options → Windows 시작 시 자동 실행**을 켜요.

- **요구 사항**: 64비트 Windows 10 2004 이상(Windows 11 권장).
- **"Windows의 PC 보호" 창이 뜨면**: 서명되지 않은 앱이라서 나오는 경고예요. **추가 정보 → 실행**을 누르면 돼요.

## 업데이트

설치판과 포터블 모두 알아서 업데이트돼요. Brim이 켜진 뒤 잠시 후, 그리고 몇 시간마다 GitHub에 새 버전이 있는지 확인해요. 있으면 바뀐 부분만 받아 두고 **다음에 켤 때** 적용해요. 바로 적용하려면 **Options → 업데이트 → 재시작해서 업데이트**를 누르면 돼요.

업데이트 확인은 GitHub에 접속하는 것 외에는 아무 정보도 보내지 않아요. 끄려면 **Options → 업데이트 자동 확인**을 꺼요.

## 사용법

- **펼치기**: 화면 맨 위 가운데로 마우스를 올리거나, 얇은 선 위에서 휠을 굴려요.
- **탭 넘기기**: 탭 이름 위에서 휠을 굴리거나 클릭해요.
- **끄기**: 선을 오른쪽 클릭 → **Brim 종료**, 또는 Options → 정보 → Brim 종료.
- 설정과 할 일은 `%AppData%\Brim\settings.json`에 저장돼요. 지우면 처음 상태로 돌아가요.

## 직접 빌드하기

.NET 8 SDK가 필요해요.

```bash
dotnet publish Brim.csproj -c Release -o out
```

새 버전 배포: 버전 태그를 올리면 GitHub Actions가 빌드하고, 설치 파일·포터블 zip·업데이트 패키지를 릴리스에 올려요. 태그 메시지가 릴리스 노트가 돼요.

```bash
git tag -a v1.1.1 -m "고친 내용"
git push origin v1.1.1
```

---

## English

Brim is a small notch-style panel that lives at the top of your Windows screen. It stays out of the way as a thin line, and springs open when you move the mouse to the top-center of the screen.

**Features**: now-playing media with controls (pick between apps when several are playing), volume and laptop brightness, screenshots (full screen, or a region via Snipping Tool) and screen recording to MP4 with system sound, a seconds clock and to-do list, CPU/memory/disk/battery stats, clipboard history (in memory only; password-manager copies are skipped), pinned apps (drag files onto the island), a wallpaper picker, power actions (restart/shut down need a second click within 3 seconds), and an Options tab (start with Windows, Korean/English, open speed, reduce motion, 12/24-hour clock, recording audio, visible tabs).

**Install**: from [the latest release](https://github.com/kisixto/Brim/releases/latest), either run **`Brim-win-Setup.exe`** (installs to `%LocalAppData%\Brim`, adds Start menu and desktop shortcuts, installs the .NET 8 and Visual C++ runtimes if missing; use `--installto "D:\Apps\Brim"` for another folder; uninstall from Settings → Apps), or unzip **`Brim-win-Portable.zip`** anywhere and run `Brim.exe` (needs the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0); you'll be prompted if it's missing). Turn on **Options → Start with Windows** to launch it at sign-in. Requires 64-bit Windows 10 2004 or later (Windows 11 recommended). Brim isn't code-signed yet, so Windows SmartScreen may warn on first launch: choose **More info → Run anyway**.

**Updates**: both the installed and portable versions update themselves. Brim checks GitHub shortly after launch and every few hours, downloads only what changed, and applies it the next time it starts (or right away via **Options → Updates → Restart to update**). The check only contacts GitHub; turn it off with **Options → Check for updates automatically**.

**Use**: hover the top-center edge (or scroll on the line) to open; scroll over the tab names to switch tabs; right-click the line → **Quit Brim** to exit. Settings live in `%AppData%\Brim\settings.json`.

**Build**: with the .NET 8 SDK, `dotnet publish Brim.csproj -c Release -o out`. To release, push a version tag (`git tag -a v1.1.1 -m "notes"`, `git push origin v1.1.1`); GitHub Actions builds and publishes the installer, portable zip and update packages.

## License

[MIT](LICENSE). Third-party components are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
