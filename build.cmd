@echo off
:: Builds Seal with the C# compiler that ships inside Windows (.NET Framework 4.8) - no SDK needed.
::   build.cmd            -> Seal.exe (real; asks for administrator rights), preview\Seal.exe, tools\omenprobe.exe
::   build.cmd preview    -> only preview\Seal.exe (same UI, simulated hardware, no elevation)
:: The exe icon (app.ico) is drawn by the app itself so the mark has a single source of truth.
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set WPF=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\WPF
set REFS=/r:System.Management.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xaml.dll /r:"%WPF%\PresentationCore.dll" /r:"%WPF%\PresentationFramework.dll" /r:"%WPF%\WindowsBase.dll"
set SRC=src\Meta.cs src\Support.cs src\Platform.cs src\Hardware.cs src\Driver.cs src\Ec.cs src\Cpu.cs src\Hid.cs src\Lighting.cs src\ExternalKeyboards.cs src\Keyboard.cs src\Display.cs src\Update.cs src\Theme.cs src\Controls.cs src\Curve.cs src\Sensors.cs src\Hotkeys.cs src\MemoryCleaner.cs src\Overlay.cs src\Engine.cs src\Ui.cs src\Program.cs
set RES=/resource:src\Ui.xaml,Seal.Ui.xaml /resource:brand\mark-512.png,Seal.brand.mark.png /resource:fonts\IBMPlexSans-Regular.ttf,Seal.fonts.IBMPlexSans-Regular.ttf /resource:fonts\IBMPlexSans-Medium.ttf,Seal.fonts.IBMPlexSans-Medium.ttf /resource:fonts\IBMPlexSans-SemiBold.ttf,Seal.fonts.IBMPlexSans-SemiBold.ttf /resource:fonts\IBMPlexMono-Regular.ttf,Seal.fonts.IBMPlexMono-Regular.ttf /resource:fonts\IBMPlexMono-Medium.ttf,Seal.fonts.IBMPlexMono-Medium.ttf /resource:fonts\OFL.txt,Seal.fonts.OFL.txt
:: Signed PawnIO modules (third_party\PawnIO.Modules, LGPL-2.1): the driver loads only these, so they travel inside the exe.
set RES=%RES% /resource:third_party\PawnIO.Modules\LpcACPIEC.bin,Seal.pawnio.LpcACPIEC.bin /resource:third_party\PawnIO.Modules\IntelMSR.bin,Seal.pawnio.IntelMSR.bin /resource:third_party\PawnIO.Modules\AMDFamily17.bin,Seal.pawnio.AMDFamily17.bin /resource:third_party\PawnIO.Modules\COPYING,Seal.pawnio.COPYING
if not exist preview mkdir preview

:: 1. preview build without an icon, used to generate app.ico
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /warn:1 /out:preview\Seal.exe /win32manifest:app.demo.manifest %RES% %REFS% %SRC%
if errorlevel 1 exit /b 1
preview\Seal.exe --make-ico app.ico
if exist app.ico (set ICO=/win32icon:app.ico) else (set ICO=)

:: 2. preview build with the icon
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /warn:1 /out:preview\Seal.exe /win32manifest:app.demo.manifest %ICO% %RES% %REFS% %SRC%
if errorlevel 1 exit /b 1
if /i "%~1"=="preview" (echo Built preview\Seal.exe & exit /b 0)

:: 3. the real thing + the probe tool
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /warn:1 /out:Seal.exe /win32manifest:app.manifest %ICO% %RES% %REFS% %SRC%
if errorlevel 1 exit /b 1
"%CSC%" /nologo /target:exe /platform:x64 /optimize+ /warn:1 /out:tools\omenprobe.exe /r:System.Management.dll tools\omenprobe.cs src\Meta.cs
if errorlevel 1 exit /b 1
echo Built Seal.exe, preview\Seal.exe and tools\omenprobe.exe
