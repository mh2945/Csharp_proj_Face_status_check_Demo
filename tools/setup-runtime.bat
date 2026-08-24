@echo off
rem ============================================================================
rem  FASMH-94 PoC — 런타임 준비 스크립트
rem
rem  하는 일
rem    1) FaceSDK zip 에서 x64 native/managed DLL 을 natives\ 로 추출
rem    2) models 폴더를 Debug / Release 출력 폴더로 복사
rem
rem  사용법
rem    setup-runtime.bat [FaceSDK zip 경로] [models 폴더 경로]
rem    인자를 생략하면 이 스크립트 기준 ..\..\ (repo 상위 폴더) 의 기본값을 쓴다.
rem
rem  라이선스
rem    models 폴더 안의 license.cer 파일만 있으면 된다 (CONTRACT.md 부록 A-1).
rem    LicenseGen.exe 를 실행하는 노드락 활성화 절차는 없다.
rem    license.cer 은 models 폴더 복사에 그대로 따라간다.
rem
rem  주의
rem    - Windows 10 1803 이상에 내장된 tar 를 쓴다.
rem    - VS 를 열어 둔 상태에서 실행했다면 natives\*.dll wildcard 를 다시 읽도록
rem      프로젝트를 '다시 로드' 해야 한다.
rem ============================================================================

chcp 65001 > nul

setlocal

set "SCRIPT_DIR=%~dp0"
set "REPO_ROOT=%SCRIPT_DIR%.."
set "PARENT_DIR=%SCRIPT_DIR%..\.."

rem --- 인자 / 기본값 ---
set "ZIP_PATH=%~1"
if "%ZIP_PATH%"=="" set "ZIP_PATH=%PARENT_DIR%\FaceSDK-1.17.7_880.20260514174316.zip"

set "MODELS_SRC=%~2"
if "%MODELS_SRC%"=="" set "MODELS_SRC=%PARENT_DIR%\models"

set "NATIVES_DIR=%REPO_ROOT%\natives"
set "TMP_DIR=%REPO_ROOT%\natives\_extract_tmp"
set "OUT_DEBUG=%REPO_ROOT%\src\EtusDetectSample\bin\x64\Debug"
set "OUT_RELEASE=%REPO_ROOT%\src\EtusDetectSample\bin\x64\Release"

rem zip 내부 경로는 항상 슬래시(/) 다.
set "SDK_IN_ZIP=lib/cpu/windows-x86_64"

echo ============================================================
echo  EtusDetectSample 런타임 준비
echo ============================================================
echo   zip    : %ZIP_PATH%
echo   models : %MODELS_SRC%
echo   출력   : %REPO_ROOT%\src\EtusDetectSample\bin\x64\{Debug,Release}
echo.

rem ---------------------------------------------------------------------------
echo [1/4] 입력 확인
rem ---------------------------------------------------------------------------
if not exist "%ZIP_PATH%" goto :err_no_zip
if not exist "%MODELS_SRC%\" goto :err_no_models

where tar >nul 2>nul
if errorlevel 1 goto :err_no_tar

if not exist "%MODELS_SRC%\license.cer" goto :err_no_license

echo       OK — zip / models / license.cer / tar 확인됨
echo.

rem ---------------------------------------------------------------------------
echo [2/4] FaceSDK 런타임 추출 (natives)
rem ---------------------------------------------------------------------------
if exist "%TMP_DIR%" rd /s /q "%TMP_DIR%"
mkdir "%TMP_DIR%"
if errorlevel 1 goto :err_mkdir_tmp

tar -xf "%ZIP_PATH%" -C "%TMP_DIR%" "%SDK_IN_ZIP%/AlcheraFaceSDK.dll" "%SDK_IN_ZIP%/opencv_world455.dll" "%SDK_IN_ZIP%/csharp/AlcheraFaceSDKCS.dll" "%SDK_IN_ZIP%/csharp/AlcheraEncryptCS.dll"
if errorlevel 1 goto :err_tar

set "EXTRACTED=%TMP_DIR%\lib\cpu\windows-x86_64"

if not exist "%NATIVES_DIR%\" mkdir "%NATIVES_DIR%"

copy /y "%EXTRACTED%\AlcheraFaceSDK.dll" "%NATIVES_DIR%\" >nul
if errorlevel 1 goto :err_copy_native

copy /y "%EXTRACTED%\opencv_world455.dll" "%NATIVES_DIR%\" >nul
if errorlevel 1 goto :err_copy_native

copy /y "%EXTRACTED%\csharp\AlcheraFaceSDKCS.dll" "%NATIVES_DIR%\" >nul
if errorlevel 1 goto :err_copy_native

copy /y "%EXTRACTED%\csharp\AlcheraEncryptCS.dll" "%NATIVES_DIR%\" >nul
if errorlevel 1 goto :err_copy_native

rd /s /q "%TMP_DIR%"

echo       OK — AlcheraFaceSDK.dll / AlcheraFaceSDKCS.dll / AlcheraEncryptCS.dll / opencv_world455.dll
echo       위치: %NATIVES_DIR%
echo.

rem ---------------------------------------------------------------------------
echo [3/4] models 복사 - Debug
rem ---------------------------------------------------------------------------
if not exist "%OUT_DEBUG%\" mkdir "%OUT_DEBUG%"

echo       0.f 하나가 260MB 입니다. 처음 실행이면 몇 분 걸립니다.
robocopy "%MODELS_SRC%" "%OUT_DEBUG%\models" /E /XO /NFL /NDL /NJH /NJS /NP
rem robocopy 는 성공해도 1~7 을 돌려준다. 8 이상만 실패로 본다.
rem (set 자체가 ERRORLEVEL 을 0 으로 바꾸므로 먼저 변수에 담아 둔다)
set "RC=%ERRORLEVEL%"
if %RC% GEQ 8 goto :err_robocopy_debug

echo       OK — %OUT_DEBUG%\models
echo.

rem ---------------------------------------------------------------------------
echo [4/4] models 복사 - Release
rem ---------------------------------------------------------------------------
if not exist "%OUT_RELEASE%\" mkdir "%OUT_RELEASE%"

robocopy "%MODELS_SRC%" "%OUT_RELEASE%\models" /E /XO /NFL /NDL /NJH /NJS /NP
set "RC=%ERRORLEVEL%"
if %RC% GEQ 8 goto :err_robocopy_release

echo       OK — %OUT_RELEASE%\models
echo.

echo ============================================================
echo  완료
echo ============================================================
echo.
echo  다음 순서로 진행하세요.
echo.
echo   1. Visual Studio 에서 EtusDetectSample.sln 을 엽니다.
echo      (이미 열려 있었다면 natives\*.dll 을 다시 읽도록 '프로젝트 다시 로드')
echo   2. 플랫폼이 x64 인지 확인하고 빌드합니다.
echo   3. EtusDetectSample.exe 를 실행합니다.
echo.
echo  라이선스는 models\license.cer 파일만 있으면 됩니다. 별도 활성화 절차는 없습니다.
echo    %OUT_DEBUG%\models\license.cer
echo.
endlocal
exit /b 0

rem ===========================================================================
rem  오류 처리
rem ===========================================================================

:err_no_zip
echo.
echo [실패] FaceSDK zip 을 찾을 수 없습니다.
echo        찾은 경로: %ZIP_PATH%
echo        사용법: setup-runtime.bat "C:\경로\FaceSDK-x.y.z.zip" "C:\경로\models"
endlocal
exit /b 1

:err_no_models
echo.
echo [실패] models 폴더를 찾을 수 없습니다.
echo        찾은 경로: %MODELS_SRC%
echo        사용법: setup-runtime.bat "C:\경로\FaceSDK-x.y.z.zip" "C:\경로\models"
endlocal
exit /b 1

:err_no_license
echo.
echo [실패] models 폴더 안에 license.cer 이 없습니다.
echo        찾은 경로: %MODELS_SRC%\license.cer
echo        이 파일이 없으면 SDK 초기화가 InvalidLicense 로 실패합니다.
echo        라이선스 파일을 models 폴더에 넣고 다시 실행하세요.
endlocal
exit /b 1

:err_no_tar
echo.
echo [실패] tar 명령을 찾을 수 없습니다.
echo        Windows 10 1803 이상이면 기본 포함되어 있습니다.
echo        구버전이라면 zip 을 수동으로 풀어 다음 파일을 natives\ 에 넣으세요.
echo          %SDK_IN_ZIP%/AlcheraFaceSDK.dll
echo          %SDK_IN_ZIP%/opencv_world455.dll
echo          %SDK_IN_ZIP%/csharp/AlcheraFaceSDKCS.dll
echo          %SDK_IN_ZIP%/csharp/AlcheraEncryptCS.dll
endlocal
exit /b 1

:err_mkdir_tmp
echo.
echo [실패] 임시 폴더를 만들지 못했습니다: %TMP_DIR%
echo        쓰기 권한을 확인하세요.
endlocal
exit /b 1

:err_tar
echo.
echo [실패] zip 추출에 실패했습니다.
echo        zip 안의 경로가 %SDK_IN_ZIP% 이 맞는지 확인하세요.
echo        확인 명령: tar -tf "%ZIP_PATH%"
if exist "%TMP_DIR%" rd /s /q "%TMP_DIR%"
endlocal
exit /b 1

:err_copy_native
echo.
echo [실패] 추출한 파일을 natives 로 복사하지 못했습니다.
echo        추출 위치: %EXTRACTED%
if exist "%TMP_DIR%" rd /s /q "%TMP_DIR%"
endlocal
exit /b 1

:err_robocopy_debug
echo.
echo [실패] models 를 Debug 출력 폴더로 복사하지 못했습니다. robocopy 코드=%RC%
echo        대상: %OUT_DEBUG%\models
echo        디스크 여유 공간(모델 약 260MB 이상)과 쓰기 권한을 확인하세요.
endlocal
exit /b 1

:err_robocopy_release
echo.
echo [실패] models 를 Release 출력 폴더로 복사하지 못했습니다. robocopy 코드=%RC%
echo        대상: %OUT_RELEASE%\models
endlocal
exit /b 1
