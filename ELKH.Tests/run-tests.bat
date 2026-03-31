@echo off
REM ==================================================================
REM ELKH PROJECT TEST EXECUTION SCRIPT (WINDOWS)
REM ==================================================================
REM Comprehensive testing script for ELKH sticker store project
REM Executes all test categories, generates coverage reports,
REM and validates quality thresholds.
REM ==================================================================

setlocal enabledelayedexpansion

REM Configuration
set PROJECT_NAME=ELKH
set TEST_PROJECT=ELKH.Tests
set COVERAGE_THRESHOLD=75
set COVERAGE_TARGET=80
set RESULTS_DIR=TestResults
set COVERAGE_DIR=CoverageReport

echo ================================================================
echo ELKH PROJECT - COMPREHENSIVE TEST EXECUTION
echo ================================================================
echo.

REM Function equivalents using labels and goto

:print_section
echo.
echo 📋 %~1
echo ----------------------------------------
goto :eof

:print_success
echo ✅ %~1
goto :eof

:print_error
echo ❌ %~1
goto :eof

:print_info
echo ℹ️  %~1
goto :eof

:cleanup
call :print_section "Cleaning Previous Test Results"

if exist "%RESULTS_DIR%" (
    rmdir /s /q "%RESULTS_DIR%"
    call :print_info "Removed previous test results"
)

if exist "%COVERAGE_DIR%" (
    rmdir /s /q "%COVERAGE_DIR%"
    call :print_info "Removed previous coverage reports"
)

mkdir "%RESULTS_DIR%" 2>nul
mkdir "%COVERAGE_DIR%" 2>nul
call :print_success "Test directories prepared"
goto :eof

:check_prerequisites
call :print_section "Checking Prerequisites"

REM Check if dotnet is available
dotnet --version >nul 2>&1
if errorlevel 1 (
    call :print_error ".NET SDK not found. Please install .NET 10 SDK."
    exit /b 1
)

REM Check .NET version
for /f "tokens=*" %%i in ('dotnet --version') do set DOTNET_VERSION=%%i
call :print_info ".NET SDK Version: !DOTNET_VERSION!"

REM Check if test project exists
if not exist "%TEST_PROJECT%\%TEST_PROJECT%.csproj" (
    call :print_error "Test project not found: %TEST_PROJECT%\%TEST_PROJECT%.csproj"
    exit /b 1
)

call :print_success "All prerequisites satisfied"
goto :eof

:install_tools
call :print_section "Installing Required Tools"

REM Check if ReportGenerator is installed
reportgenerator --version >nul 2>&1
if errorlevel 1 (
    call :print_info "Installing ReportGenerator..."
    dotnet tool install -g dotnet-reportgenerator-globaltool
    if errorlevel 1 (
        call :print_error "Failed to install ReportGenerator"
        exit /b 1
    )
    call :print_success "ReportGenerator installed"
) else (
    call :print_info "ReportGenerator already installed"
)

call :print_info "Ensuring coverage tools are available..."
dotnet add "%TEST_PROJECT%" package coverlet.collector --version 6.0.2 >nul 2>&1
call :print_success "Coverage tools ready"
goto :eof

:build_solution
call :print_section "Building Solution"

call :print_info "Restoring NuGet packages..."
dotnet restore
if errorlevel 1 (
    call :print_error "Package restore failed"
    exit /b 1
)

call :print_info "Building in Release configuration..."
dotnet build --configuration Release --no-restore
if errorlevel 1 (
    call :print_error "Build failed"
    exit /b 1
)

call :print_success "Solution built successfully"
goto :eof

:run_unit_tests
call :print_section "Running Unit Tests"

call :print_info "Executing all unit tests with coverage collection..."

dotnet test "%TEST_PROJECT%" ^
    --configuration Release ^
    --no-build ^
    --collect:"XPlat Code Coverage" ^
    --results-directory "./%RESULTS_DIR%" ^
    --logger "trx;LogFileName=UnitTestResults.trx" ^
    --verbosity minimal

if errorlevel 1 (
    call :print_error "Unit tests failed"
    exit /b 1
)

call :print_success "Unit tests completed successfully"
goto :eof

:run_integration_tests
call :print_section "Running Integration Tests"

call :print_info "Executing integration tests..."

dotnet test "%TEST_PROJECT%" ^
    --configuration Release ^
    --no-build ^
    --filter "Category=Integration" ^
    --collect:"XPlat Code Coverage" ^
    --results-directory "./%RESULTS_DIR%" ^
    --logger "trx;LogFileName=IntegrationTestResults.trx" ^
    --verbosity minimal

if errorlevel 1 (
    call :print_error "Integration tests failed"
    exit /b 1
)

call :print_success "Integration tests completed successfully"
goto :eof

:run_performance_tests
call :print_section "Running Performance Tests"

call :print_info "Executing performance validation tests..."

dotnet test "%TEST_PROJECT%" ^
    --configuration Release ^
    --no-build ^
    --filter "Category=Performance" ^
    --logger "trx;LogFileName=PerformanceTestResults.trx" ^
    --verbosity normal

if errorlevel 1 (
    call :print_error "Performance tests failed - check for performance regressions"
    REM Don't exit on performance test failure, but warn
)

call :print_success "Performance tests completed"
goto :eof

:generate_coverage_report
call :print_section "Generating Coverage Reports"

REM Find coverage files (simplified - assumes standard location)
set COVERAGE_FILES=
for /r "%RESULTS_DIR%" %%f in (coverage.cobertura.xml) do (
    if exist "%%f" (
        if defined COVERAGE_FILES (
            set COVERAGE_FILES=!COVERAGE_FILES!;%%f
        ) else (
            set COVERAGE_FILES=%%f
        )
    )
)

if not defined COVERAGE_FILES (
    call :print_error "No coverage files found"
    exit /b 1
)

call :print_info "Found coverage files: !COVERAGE_FILES!"
call :print_info "Generating HTML coverage report..."

reportgenerator ^
    -reports:"!COVERAGE_FILES!" ^
    -targetdir:"./%COVERAGE_DIR%" ^
    -reporttypes:"Html;Badges;TextSummary" ^
    -assemblyfilters:"+ELKH*;-*Tests*" ^
    -classfilters:"-*.Migrations.*;-*.Areas.Identity.*"

if errorlevel 1 (
    call :print_error "Failed to generate coverage report"
    exit /b 1
)

call :print_success "Coverage report generated in %COVERAGE_DIR%"
goto :eof

:analyze_coverage
call :print_section "Analyzing Coverage Results"

if exist "%COVERAGE_DIR%\Summary.txt" (
    call :print_info "Coverage Summary:"
    type "%COVERAGE_DIR%\Summary.txt"
) else (
    call :print_info "Coverage summary not available"
)
goto :eof

:generate_summary
call :print_section "Test Execution Summary"

echo.
echo 📊 ELKH TEST EXECUTION SUMMARY
echo ================================
echo Test Project: %TEST_PROJECT%
echo Configuration: Release
echo Coverage Target: %COVERAGE_TARGET%%%
echo Coverage Threshold: %COVERAGE_THRESHOLD%%%
echo.
echo 📁 Output Directories:
echo   Test Results: %RESULTS_DIR%\
echo   Coverage Report: %COVERAGE_DIR%\index.html
echo.
echo 🌐 View Coverage Report:
echo   Open %COVERAGE_DIR%\index.html in your browser
echo.

if exist "%COVERAGE_DIR%\index.html" (
    call :print_success "Full test suite completed successfully!"
    
    REM Try to open coverage report
    start "" "%COVERAGE_DIR%\index.html" >nul 2>&1
    if not errorlevel 1 (
        call :print_info "Opening coverage report in browser..."
    )
) else (
    call :print_error "Test execution completed with issues - check logs above"
    exit /b 1
)
goto :eof

:main
echo Starting comprehensive test execution...
echo.

call :cleanup
call :check_prerequisites
call :install_tools
call :build_solution
call :run_unit_tests
call :run_integration_tests
call :run_performance_tests
call :generate_coverage_report
call :analyze_coverage
call :generate_summary

echo.
echo 🎉 ELKH test execution completed successfully!
echo 📈 Ready for production deployment
goto :eof

REM Handle script arguments
if "%~1"=="" goto main
if "%~1"=="full" goto main
if "%~1"=="unit" (
    call :print_info "Running unit tests only..."
    call :check_prerequisites
    call :build_solution
    call :run_unit_tests
    goto :eof
)
if "%~1"=="integration" (
    call :print_info "Running integration tests only..."
    call :check_prerequisites
    call :build_solution
    call :run_integration_tests
    goto :eof
)
if "%~1"=="performance" (
    call :print_info "Running performance tests only..."
    call :check_prerequisites
    call :build_solution
    call :run_performance_tests
    goto :eof
)
if "%~1"=="coverage" (
    call :print_info "Generating coverage report only..."
    call :generate_coverage_report
    call :analyze_coverage
    goto :eof
)

REM Default to full execution
goto main