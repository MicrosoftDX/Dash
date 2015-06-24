@echo off
setlocal
set logfile=%StartupLogs%\RunRulesConfigurator.log

echo %date% %time% >> %logfile% 2>&1
echo %cd% >> %logfile% 2>&1

schtasks /Create /F /RU "NT AUTHORITY\SYSTEM" /SC MINUTE /TN RunWorkerQueue /TR "'%cd%\DashAsync.exe'" >> %logfile% 2>&1
exit /b 0