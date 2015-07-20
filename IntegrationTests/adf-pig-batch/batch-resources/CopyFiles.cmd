echo.
echo Moving Startup files to shared task directory
robocopy /MIR .\ %WATASK_TVM_ROOT_DIR%\shared
if "%errorlevel%" LEQ "4" (
    SET errorlevel=0
)
