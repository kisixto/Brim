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
| **Options** | Windows 시작 시 자동 실행, 언어(한국어/English), 펼치는 속도, 애니메이션 줄이기, 12/24시간제, 녹화 소리, 보이는 탭 |

## 설치

1. [Releases](https://github.com/kisixto/Brim/releases)에서 `Brim-<버전>-win-x64.zip`을 받아 원하는 폴더에 압축을 풀어요.
2. `Brim.exe`를 실행해요. 화면 위쪽 가운데에 얇은 검은 선이 생기면 성공이에요.
3. Windows를 켤 때마다 실행하려면 **Options → Windows 시작 시 자동 실행**을 켜요.

- **요구 사항**: Windows 10 2004 이상(Windows 11 권장), 64비트. .NET은 zip에 들어 있어서 따로 설치할 필요 없어요. 녹화가 안 되면 [Visual C++ 재배포 패키지 (x64)](https://aka.ms/vs/17/release/vc_redist.x64.exe)를 설치해 주세요.
- **"Windows의 PC 보호" 창이 뜨면**: 서명되지 않은 앱이라서 나오는 경고예요. **추가 정보 → 실행**을 누르면 돼요.

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

배포용(.NET 포함) 빌드:

```bash
dotnet publish Brim.csproj -c Release --self-contained true -o dist/Brim
```

---

## English

Brim is a small notch-style panel that lives at the top of your Windows screen. It stays out of the way as a thin line, and springs open when you move the mouse to the top-center of the screen.

**Features**: now-playing media with controls (pick between apps when several are playing), volume and laptop brightness, screenshots (full screen, or a region via Snipping Tool) and screen recording to MP4 with system sound, a seconds clock and to-do list, CPU/memory/disk/battery stats, clipboard history (in memory only; password-manager copies are skipped), pinned apps (drag files onto the island), a wallpaper picker, power actions (restart/shut down need a second click within 3 seconds), and an Options tab (start with Windows, Korean/English, open speed, reduce motion, 12/24-hour clock, recording audio, visible tabs).

**Install**: download `Brim-<version>-win-x64.zip` from [Releases](https://github.com/kisixto/Brim/releases), unzip it anywhere, and run `Brim.exe`. Turn on **Options → Start with Windows** to launch it at sign-in. Requires 64-bit Windows 10 2004 or later (Windows 11 recommended); .NET is included. If recording fails, install the [Visual C++ Redistributable (x64)](https://aka.ms/vs/17/release/vc_redist.x64.exe). Brim isn't code-signed yet, so Windows SmartScreen may warn on first launch: choose **More info → Run anyway**.

**Use**: hover the top-center edge (or scroll on the line) to open; scroll over the tab names to switch tabs; right-click the line → **Quit Brim** to exit. Settings live in `%AppData%\Brim\settings.json`.

**Build**: with the .NET 8 SDK, `dotnet publish Brim.csproj -c Release -o out`.

## License

[MIT](LICENSE). Third-party components are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
