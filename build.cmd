@echo off
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /nowarn:1690 /target:winexe /win32icon:"%~dp0MultiCursor.ico" /optimize+ /out:"%~dp0MultiCursor.exe" "%~dp0MultiCursor.cs"
