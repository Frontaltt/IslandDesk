# Island Desk 1.3.1

**A customizable Dynamic Island-style desktop overlay for Windows 10/11.** Island Desk brings glanceable system information, media controls and notification previews into a compact floating capsule, with a separate Studio app for configuration.

[English](#english) · [Русский](#русский)

---

## English

### Overview

Island Desk is a lightweight desktop overlay inspired by the interaction model of macOS Dynamic Island. It is built with C# and WPF and includes two applications:

- **IslandDesk.exe** — the floating overlay.
- **IslandDesk.Studio.exe** — the settings editor.

This is a desktop application. It is not a game injector, graphics hook, driver or system replacement.

### Features

- Floating top-centre capsule with adjustable width, corner radius, transparency, tint and accent colour.
- Dark and light themes. English is the default UI language; Russian can be selected in Studio.
- Configurable CPU, RAM, GPU, FPS, CPU/GPU temperature, clock and media widgets.
- Reorderable widgets in the compact and expanded layouts.
- Notification previews that slide into view and expand to show details when clicked.
- Gaming mode with a compact indicator, an optional single system metric and notification interaction.
- Media controls: previous track, play/pause and next track; album artwork from Windows media sessions.
- Hover-revealed quick actions in the brand area and hover-revealed music controls.
- A 10-band equalizer settings preview linked to Equalizer APO configuration.
- Optional capsule, notification and brand animations.
- **Island Desk Studio** for appearance, widget order, gaming rules, app exclusions, hotkeys and audio settings.

### Screens and components

| Component | Project / file | Purpose |
| --- | --- | --- |
| Overlay | `IslandDesk.csproj` | Floating island and desktop widgets |
| Studio | `Studio/IslandDesk.Studio.csproj` | Configuration UI |
| Solution | `IslandDesk.sln` | Builds both applications |
| Packaging | `BuildPackage.ps1` | Creates and locally signs an MSIX package |
| Portable publish | `BuildAll.ps1` | Publishes both EXE applications to `dist` |

### Requirements

- Windows 10 version 2004 or later; Windows 11 is recommended.
- .NET 8 SDK.
- Windows SDK tools (`MakeAppx.exe` and `SignTool.exe`) only if you want to create the MSIX package.

Optional integrations:

- **RTSS (RivaTuner Statistics Server)** for FPS readings. RTSS must be running and its shared-memory support available.
- **Equalizer APO** for actual system-wide equalizer processing. Island Desk edits its configuration; it is not an audio driver or DSP engine.
- Hardware temperature readings depend on sensors exposed by the device, firmware and drivers through LibreHardwareMonitor.

### Build from source

Open PowerShell in the repository root:

```powershell
dotnet restore .\IslandDesk.sln
dotnet build .\IslandDesk.sln -c Release
```

To publish both applications as self-contained executables:

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAll.ps1
```

The result is placed in `dist`. Keep the full directory together; do not move an EXE away from its accompanying runtime files and dependencies.

### Create and install the MSIX package

MSIX is the recommended installation route for the Windows notification-listener capability. From an elevated PowerShell window, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildPackage.ps1
```

The script publishes both apps, creates `IslandDesk.msix` and signs it with a **local development certificate**. This is a self-signed test package, not a Microsoft Store-signed release. Review the certificate name and prompts before trusting it. Never distribute your private `.pfx` file. Certificate and build outputs are excluded by `.gitignore`.



### Integrations and compatibility notes

- **Fullscreen games:** the WPF `Topmost` overlay works over ordinary windows and borderless-windowed games. Exclusive DirectX fullscreen may cover normal desktop windows. Use *Borderless Windowed* for the most reliable overlay behaviour. Island Desk does not inject into games or hook rendering APIs.
- **Glass appearance:** opacity, tint, border and shadow create a glass-style surface. This is not a guaranteed live Acrylic/backdrop blur of the desktop.
- **CPU/GPU temperatures:** values are available only when the hardware and driver expose a sensor. Some systems may require elevated access. A missing sensor is shown as `—°C`; the app should not substitute an unrelated temperature.
- **GPU load:** depends on Windows GPU Engine performance counters and driver support.
- **FPS:** requires RTSS shared-memory support and an active RTSS profile for the game.
- **Notifications:** requires an installed MSIX package, Windows notification permission and the `userNotificationListener` capability. The app receives new Windows notifications; it does not download private chat history from Discord, Telegram or other services.
- **Music:** metadata and transport controls require the player to publish a Windows media session.
- **Equalizer:** actual audio processing is performed by Equalizer APO. The active Windows output device must be configured in Equalizer APO Configurator.

### Settings and privacy

Settings are stored locally at `%AppData%\IslandDesk\settings.json`. Notification content is read locally through the Windows notification listener and displayed on the desktop. The project does not include analytics or telemetry code. Do not commit generated certificates, private keys, personal settings or build output.

### Development status

Version: **1.3.1**. This is an actively iterated personal project. The repository includes a GitHub Actions workflow for Windows build validation, but every release should still be tested on a real Windows machine before distribution. No `LICENSE` file is included yet; choose and add a license before accepting third-party contributions or advertising the project as open source.

### Contributing

Issues and pull requests are welcome. Please include your Windows version, .NET SDK version, reproduction steps and relevant build or runtime logs when reporting a problem. Avoid attaching personal notification content, certificates or private configuration files.

---

## Русский

### О проекте

**Island Desk** — настраиваемый плавающий островок для Windows 10/11 в стиле Dynamic Island на macOS. Он объединяет системные показатели, управление музыкой и центр уведомлений в компактной капсуле. Для настройки предусмотрено отдельное приложение Studio.

В проект входят две программы:

- **IslandDesk.exe** — плавающий островок поверх рабочего стола.
- **IslandDesk.Studio.exe** — редактор внешнего вида и функций.

Это обычное desktop-приложение: оно не внедряется в игры, не подменяет драйверы и не перехватывает графический рендеринг.

### Возможности

- Капсула в верхней части экрана с настройкой ширины, скругления, прозрачности, оттенка и акцентного цвета.
- Тёмная и светлая темы. Английский язык включён по умолчанию; русский выбирается в Studio.
- Виджеты CPU, RAM, GPU, FPS, температуры CPU/GPU, часов и музыки.
- Изменение порядка виджетов в компактном и раскрытом режимах.
- Уведомления плавно появляются в виде небольшой карточки и раскрываются по нажатию.
- Игровой режим с компактным индикатором, одним выбранным системным показателем и уведомлениями.
- Управление музыкой: предыдущий трек, пауза/воспроизведение, следующий трек и обложка из медиасессии Windows.
- Быстрые действия появляются при наведении на область названия; музыкальные кнопки — при наведении на музыкальный блок.
- Предпросмотр 10-полосного эквалайзера, связанный с настройками Equalizer APO.
- Необязательные анимации островка, уведомлений и надписи Island Desk.
- **Island Desk Studio** для настройки внешнего вида, порядка виджетов, игрового режима, исключений приложений, хоткеев и аудио.

### Структура проекта

| Компонент | Проект / файл | Назначение |
| --- | --- | --- |
| Островок | `IslandDesk.csproj` | Плавающее окно и виджеты |
| Studio | `Studio/IslandDesk.Studio.csproj` | Редактор настроек |
| Решение | `IslandDesk.sln` | Сборка обеих программ |
| MSIX | `BuildPackage.ps1` | Создание и локальная подпись установочного пакета |
| EXE | `BuildAll.ps1` | Публикация двух приложений в `dist` |

### Требования

- Windows 10 версии 2004 или новее; рекомендуется Windows 11.
- .NET 8 SDK.
- Windows SDK (`MakeAppx.exe` и `SignTool.exe`) — только для создания MSIX.

Дополнительные интеграции:

- **RTSS (RivaTuner Statistics Server)** — для FPS. RTSS должен быть запущен, а поддержка shared memory доступна.
- **Equalizer APO** — для настоящей системной обработки звука. Island Desk изменяет конфигурацию APO, но сам не является аудиодрайвером или DSP-движком.
- Температуры зависят от датчиков, которые устройство, прошивка и драйверы предоставляют через LibreHardwareMonitor.

### Сборка из исходников

Открой PowerShell в корне репозитория:

```powershell
dotnet restore .\IslandDesk.sln
dotnet build .\IslandDesk.sln -c Release
```

Чтобы опубликовать обе программы как самостоятельные EXE:

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAll.ps1
```

Результат появится в папке `dist`. Не отделяй EXE от остальных файлов: рядом должны оставаться runtime-файлы и зависимости.

### Создание и установка MSIX

Для доступа к системным уведомлениям рекомендуется установка через MSIX. Открой PowerShell от имени администратора и выполни:

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildPackage.ps1
```

Скрипт публикует обе программы, создаёт `IslandDesk.msix` и подписывает его **локальным сертификатом разработки**. Это тестовая самоподписанная сборка, а не пакет, подписанный Microsoft Store. Перед доверием сертификату проверь его имя и запросы установщика. Никогда не распространяй закрытый ключ `.pfx`. Сертификаты и результаты сборки исключены из Git через `.gitignore`.



### Интеграции и ограничения

- **Полноэкранные игры:** `Topmost` работает поверх обычных окон и игр в режиме Borderless Windowed. Exclusive Fullscreen DirectX может перекрыть обычное WPF-окно. Для надёжного отображения используй *Безрамочный оконный режим*. Island Desk не внедряется в игры и не перехватывает графический рендеринг.
- **Стеклянная тема:** прозрачность, оттенок, рамка и тень создают стеклянный вид. Настоящее живое Acrylic-размытие рабочего стола не гарантируется.
- **Температуры CPU/GPU:** доступны только датчики, которые отдают оборудование и драйвер. На некоторых системах могут потребоваться повышенные права. Если датчик отсутствует, отображается `—°C`; программа не должна подставлять температуру другого компонента.
- **Загрузка GPU:** зависит от счётчиков Windows GPU Engine и поддержки драйвера.
- **FPS:** требуется RTSS с поддержкой shared memory и активным профилем игры.
- **Уведомления:** нужны установленный MSIX, разрешение Windows и capability `userNotificationListener`. Программа получает новые системные уведомления, но не загружает личную историю чатов Discord, Telegram и других сервисов.
- **Музыка:** для метаданных и управления плеер должен предоставлять медиасессию Windows.
- **Эквалайзер:** настоящую обработку выполняет Equalizer APO. В Configurator должно быть выбрано активное устройство вывода Windows.

### Настройки и приватность

Настройки хранятся локально в `%AppData%\IslandDesk\settings.json`. Текст уведомлений читается локально через Windows Notification Listener и отображается на рабочем столе. В проекте нет кода аналитики или телеметрии. Не добавляй в Git сертификаты, закрытые ключи, личные настройки и результаты сборки.

### Статус разработки

Версия: **1.3.1**. Проект продолжает развиваться. В репозитории есть GitHub Actions для проверки сборки под Windows, однако каждый релиз всё равно нужно тестировать на реальном компьютере с Windows. Файл `LICENSE` пока не добавлен: выбери и добавь лицензию до приёма сторонних изменений или объявления проекта open source.

### Участие в разработке

Можно создавать Issues и Pull Requests. При сообщении об ошибке указывай версию Windows, версию .NET SDK, шаги воспроизведения и соответствующий лог сборки или работы программы. Не прикладывай текст личных уведомлений, сертификаты и приватные настройки.
